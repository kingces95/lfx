using System;
using System.Net;
using System.Threading;
using System.Text;

namespace Lfx {
    public class WebServer {
        private readonly HttpListener m_listener = new HttpListener();
        private readonly Func<HttpListenerRequest, object> m_responderMethod;
        private readonly string m_name;

        public WebServer(
            string name,
            string[] prefixes, 
            Func<HttpListenerRequest, object> method) {

            m_name = name;

            if (!HttpListener.IsSupported)
                throw new NotSupportedException(
                    "Needs Windows XP SP2, Server 2003 or later.");

            // URI prefixes are required, for example 
            // "http://localhost:8080/index/".
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("prefixes");

            // A responder method is required
            if (method == null)
                throw new ArgumentException("method");

            foreach (string s in prefixes)
                m_listener.Prefixes.Add(s);

            m_responderMethod = method;
            m_listener.Start();
        }

        public WebServer(
            string name,
            Func<HttpListenerRequest, object> method, 
            params string[] prefixes)
            : this(name, prefixes, method) { }

        private void HandleRequest(object context) {
            var ctx = context as HttpListenerContext;
            try {
                var response = m_responderMethod(ctx.Request);
                var buf = response as byte[];
                if (response is string)
                    buf = Encoding.UTF8.GetBytes((string)response);
                ctx.Response.ContentLength64 = buf.Length;

                ctx.Response.SendChunked = false;
                ctx.Response.ContentType = "application/vnd.git-lfs+json";
                ctx.Response.StatusCode = 200;
                ctx.Response.AddHeader("status", "200");

                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
            } finally {
                ctx.Response.OutputStream.Flush();
                ctx.Response.OutputStream.Close();
            }
        }

        public void Run() {
            ThreadPool.QueueUserWorkItem((o) => {
                Console.WriteLine($"Webserver '{m_name}' running...");
                try {
                    while (m_listener.IsListening) {
                        ThreadPool.QueueUserWorkItem(
                            HandleRequest, m_listener.GetContext());
                    }

                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            });
        }

        public void Stop() {
            m_listener.Stop();
            m_listener.Close();
        }
    }
}