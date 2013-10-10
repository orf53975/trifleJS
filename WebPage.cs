﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.IO;
using System.Net;
using System.Windows.Forms;
using mshtml;

namespace TrifleJS
{
    /// <summary>
    /// Encapsulates a webpage opened inside IE environment
    /// </summary>
    public class WebPage
    {
        private Browser browser;

        public WebPage() {
            this.browser = new Browser();
            this.browser.Size = new Size(1024, 800);
            this.browser.ScrollBarsEnabled = false;
            this.browser.ObjectForScripting = new Callback.External();
        }

        /// <summary>
        /// Takes a screenshot and saves into a file path
        /// </summary>
        /// <param name="filename">path where the screenshot is saved</param>
        public void Render(string filename)
        {
            browser.Render(filename);
        }

        /// <summary>
        /// Takes a screenshot and returns Base64 encoding
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public string RenderBase64(string format) { 
            using (var pic = browser.Render())
            {
                MemoryStream stream = new MemoryStream();
                switch (format.ToUpper()) { 
                    case "JPG":
                        pic.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                        break;
                    case "GIF":
                        pic.Save(stream, System.Drawing.Imaging.ImageFormat.Gif);
                        break;
                    default:
                        pic.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        break;
                }
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        
        /// <summary>
        /// Opens a url and executes a callback
        /// </summary>
        /// <param name="url">URL location</param>
        /// <param name="callbackId">id of the callback to execute</param>
        public void Open(string url, string callbackId) {
            Console.WriteLine("Opening " + url);
            // Check the URL
            if (TryParse(url) != null)
            {
                browser.Navigate(url);
                browser.DocumentCompleted += delegate
                {
                    // Add toolset
                    EvaluateJavaScript(TrifleJS.Properties.Resources.ie_toolset);
                    // Continue with callback
                    Callback.execute(callbackId, "success");
                };
                while (browser.ReadyState != System.Windows.Forms.WebBrowserReadyState.Complete)
                {
                    System.Windows.Forms.Application.DoEvents();
                }
            }
            else {
                Console.WriteLine("Error opening url: " + url);
            }
        }

        /// <summary>
        /// Tries to parse a URL, otherwise returns null
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private Uri TryParse(string url) {
            Uri uri;
            try { uri = new Uri(url); }
            catch { return null; }
            return uri;
        }

        /// <summary>
        /// Evaluates (executes) javascript on the currently open window
        /// @see http://stackoverflow.com/questions/153748/how-to-inject-javascript-in-webbrowser-control
        /// </summary>
        /// <param name="code">code to inject into browser window</param>
        public void EvaluateJavaScript(string code)
        {
            HtmlElementCollection head = browser.Document.GetElementsByTagName("head");
            if (head != null)
            {
                try
                {
                    HtmlElement scriptEl = browser.Document.CreateElement("script");
                    IHTMLScriptElement element = (IHTMLScriptElement)scriptEl.DomElement;
                    element.text = code;
                    head[0].AppendChild(scriptEl);
                }
                catch (Exception ex){
                    Utils.Debug(ex.Message);
                }
            }
        }

        /// <summary>
        /// Evaluates (executes) a javascript function and returns result
        /// </summary>
        /// <param name="function">javascript function to execute</param>
        /// <param name="args">arguments to pass the the function</param>
        /// <returns></returns>
        public object Evaluate(string function, object[] args)
        {
            string[] input;
            if (args == null) { input = new string[] {}; }
            else { input = Callback.parse(args); }
            string guid = "__" + (Guid.NewGuid()).ToString().Replace("-", "");
            string script = String.Format("function {0}() {{ return ({1})({2}); }}", guid, function, String.Join(",", input));
            EvaluateJavaScript(script);
            object result = browser.Document.InvokeScript(guid);
            return result;
        }

        /// <summary>
        /// Includes a JavaScript file from external URL and executes a 
        /// callback when ready
        /// </summary>
        /// <param name="url">URL of javascript file</param>
        /// <param name="callbackId"></param>
        public void IncludeJs(string url, string callbackId)
        {
            Uri uri = TryParse(url);
            if (uri != null)
            {
                HtmlElementCollection head = browser.Document.GetElementsByTagName("head");
                if (head != null)
                {
                    HtmlElement scriptEl = browser.Document.CreateElement("script");
                    IHTMLScriptElement element = (IHTMLScriptElement)scriptEl.DomElement;
                    element.src = url;
                    element.type = "text/javascript";
                    head[0].AppendChild(scriptEl);
                    // Listen for readyState changes
                    ((mshtml.HTMLScriptEvents2_Event)element).onreadystatechange += delegate
                    {
                        if (element.readyState == "complete" || element.readyState == "loaded")
                        {
                            Callback.execute(callbackId);
                        }
                    };
                }
            }
        }

        public class CallbackEventArgs : EventArgs {
            public string callbackId;
            public CallbackEventArgs(string callbackId)
            {
                this.callbackId = callbackId;
            }
        }

        public void callbackHandler(object sender, CallbackEventArgs e)
        {
            Callback.execute(e.callbackId);
        }

        /// <summary>
        /// Injects a JavaScript file into current active window
        /// </summary>
        /// <param name="filename">path of the javascript file to inject</param>
        public void InjectJs(string filename) {
            if (File.Exists(filename)) {
                EvaluateJavaScript(File.ReadAllText(filename));
            }
        }
    }
}
