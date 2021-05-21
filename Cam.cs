using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace demo
{
    public class Cam
    {
        object locker = new object();
        bool signaledToStop = false;
        List<StreamingSession> sessions = new List<StreamingSession>();
        VideoCaptureDevice finalVideo;

        public Cam()
        {
            FilterInfoCollection videoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            finalVideo = new VideoCaptureDevice(videoCaptureDevices[0].MonikerString);
            
            finalVideo.VideoResolution = finalVideo.VideoCapabilities
                .OrderByDescending(x => x.MaximumFrameRate)
                .ThenByDescending(x=>x.FrameSize.Width)
                .FirstOrDefault();

            finalVideo.NewFrame += this._streamNewFrame;
        }

        private void _streamNewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            System.Drawing.Image imgforms = (Bitmap)eventArgs.Frame.Clone();
            byte[] data = new byte[0];

            using (MemoryStream stream = new MemoryStream())
            {
                imgforms.Save(stream, ImageFormat.Jpeg);
                data = stream.ToArray();
            }

            lock (this.locker)
            {
                foreach (var session in sessions.ToList())
                {
                    session.ProvideData(data);
                }
            }
        }

        public StreamingSession StreamOn(Action<byte[]> callback)
        {
            StreamingSession session = new StreamingSession(callback);
            lock (this.locker)
            {
                this.sessions.Add(session);

                if (this.signaledToStop)
                {
                    this.finalVideo.WaitForStop();
                }

                if (!this.finalVideo.IsRunning)
                {
                    this.finalVideo.Start();
                    this.signaledToStop = false;
                }
            }

            session.OnSessionEnded += Session_OnSessionEnded;

            return session;
        }

        private void Session_OnSessionEnded(object sender, EventArgs e)
        {
            lock(this.locker)
            {
                this.sessions.Remove(sender as StreamingSession);

                if (!this.sessions.Any())
                {
                    this.finalVideo.SignalToStop();
                    this.signaledToStop = true;
                }
            }            
        }
    }

    public class StreamingSession
    {
        public StreamingSession(Action<byte[]> Callback)
        {
            this.Callback = Callback;
        }

        private Action<byte[]> Callback;
        private TaskCompletionSource Completion = new TaskCompletionSource();

        public event EventHandler OnSessionEnded;

        public Task WaitAsync(int? timeout = null)
        {
            if (timeout.HasValue)
            {
                return Task.WhenAny(Task.Delay(timeout.Value), this.Completion.Task);
            }

            return this.Completion.Task;
        }

        public void ProvideData(byte[] data)
        {
            try
            {
                this.Callback(data);
            }
            catch(Exception)
            {
                this.EndSession();
            }
        }

        public void EndSession()
        {
            this.Completion.SetResult();
            if (this.OnSessionEnded != null)
            {
                this.OnSessionEnded(this, null);
            }
        }
    }
}
