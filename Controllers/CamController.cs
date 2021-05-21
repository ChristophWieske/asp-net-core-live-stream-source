using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace demo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CamController : ControllerBase
    {
        private readonly Cam cam;

        public CamController(Cam cam)
        {
            this.cam = cam;
        }

        [HttpGet]
        [Route("cam/video")]
        public async Task Get()
        {
            Response.ContentType = "video/webm";
            // How to go on here? 
        }

        [HttpGet]
        [Route("cam/mjpeg")]
        public async Task Get2()
        {
            Response.StatusCode = 206;
            Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
            Response.Headers.Add("Connection", "Keep-Alive");
            

            StreamingSession session = this.cam.StreamOn(data =>
                {
                    if (Request.HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        throw new Exception();
                    }

                    Response.Body.Write(this.CreateHeader(data.Length));
                    Response.Body.Write(data);
                    Response.Body.Write(this.CreateFooter());
                    Response.Body.Flush();
                });

            await Response.StartAsync();

            await session.WaitAsync();
        }

        /// <summary>
        /// Create an appropriate header.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private byte[] CreateHeader(int length)
        {
            string header =
                "--frame" + "\r\n" +
                "Content-Type:image/jpeg\r\n" +
                "Content-Length:" + length + "\r\n\r\n";

            return Encoding.ASCII.GetBytes(header);
        }

        private byte[] CreateFooter()
        {
            return Encoding.ASCII.GetBytes("\r\n");
        }
    }
}
