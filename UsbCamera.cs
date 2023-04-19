using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Size = System.Windows.Size;
using System.Drawing;
using System.Threading.Tasks;
using System.Management;

namespace ALPR
{
    public class UsbCamera
    {
        public Size Size { get; private set; }

        public Action Start { get; private set; }

        public Action Stop { get; private set; }

        public Action Pause { get; private set; }

        public bool IsRunning { get; private set; } = false;

        public bool IsPause { get; set; } = false;

        public delegate void Event(Bitmap bmp);
        public event Event NewFrame;

        public delegate void Event2(string Description);
        public event Event2 VideoSourceError;
        public event Event2 PlayingFinished;
        private static FilterInfoCollection deviceLists;

        public UsbCamera(string MonikerString)
        {
            int cameraIndex = deviceLists[MonikerString];
            if (cameraIndex < 0)
            {
                VideoSourceError?.Invoke("Not available");
            }
            else
            {
                Init(cameraIndex);
            }
        }

        public static FilterInfoCollection FindDevices()
        {
            return deviceLists = new FilterInfoCollection();
        }

        private static VideoFormat[] GetVideoFormat(int cameraIndex)
        {
            DirectShow.IBaseFilter filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory, cameraIndex);
            DirectShow.IPin pin = DirectShow.FindPin(filter, 0, DirectShow.PIN_DIRECTION.PINDIR_OUTPUT);
            return GetVideoOutputFormat(pin);
        }

        private void Init(int cameraIndex)
        {
            VideoFormat[] formats = GetVideoFormat(cameraIndex);
            DirectShow.IBaseFilter vcap_source = CreateVideoCaptureSource(cameraIndex, formats[cameraIndex]);

            DirectShow.IGraphBuilder graph = DirectShow.CreateGraph();
            _ = graph.AddFilter(vcap_source, "VideoCapture");

            DirectShow.ICaptureGraphBuilder2 builder = DirectShow.CoCreateInstance(DirectShow.DsGuid.CLSID_CaptureGraphBuilder2) as DirectShow.ICaptureGraphBuilder2;
            _ = builder.SetFiltergraph(graph);

            SampleGrabberInfo sample1 = ConnectSampleGrabberAndRenderer(graph, builder, vcap_source, DirectShow.DsGuid.PIN_CATEGORY_CAPTURE);
            if (sample1 != null)
            {
                SampleGrabberCallback sampler = new SampleGrabberCallback(sample1.Grabber, sample1.Width, sample1.Height, sample1.Stride, false);

                Size = new Size(sample1.Width, sample1.Height);

                Start = () =>
                {
                    if (IsRunning == false)
                    {
                        if (IsPause)
                        {
                            DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Running);
                            sampler.Buffered += Sampler_Buffered;
                            IsPause = false;
                        }
                        else
                        {
                            sampler.Buffered += Sampler_Buffered;
                            sampler.Error += Sampler_Error;
                            sampler.Finished += Sampler_Finished;
                            sampler.SetStart(formats[cameraIndex].ToString());

                            DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Running);
                            IsRunning = true;
                            IsPause = false;
                        }
                    }
                };

                Stop = () =>
                {
                    if (IsRunning)
                    {
                        DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Stopped);
                        sampler.SetStop();
                        IsRunning = false;
                        IsPause = false;
                    }
                };

                Pause = () =>
                {
                    if (IsRunning)
                    {
                        if (IsPause == false)
                        {
                            DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Paused);
                            sampler.Buffered -= Sampler_Buffered;
                            IsPause = true;
                        }
                    }
                };
            }
        }

        private void Sampler_Finished(SampleGrabberCallback sample, string description)
        {
            PlayingFinished?.Invoke(description);
            sample.Buffered -= Sampler_Buffered;
            sample.Error -= Sampler_Error;
            sample.Finished -= Sampler_Finished;
        }

        private void Sampler_Error(SampleGrabberCallback sample, string description)
        {
            VideoSourceError?.Invoke(description);
        }

        private void Sampler_Buffered(Bitmap bmp)
        {
            NewFrame?.Invoke(bmp);
        }

        private SampleGrabberInfo ConnectSampleGrabberAndRenderer(DirectShow.IFilterGraph graph, DirectShow.ICaptureGraphBuilder2 builder, DirectShow.IBaseFilter vcap_source, Guid pinCategory)
        {
            DirectShow.IBaseFilter grabber = CreateSampleGrabber();
            _ = graph.AddFilter(grabber, "SampleGrabber");

            DirectShow.ISampleGrabber i_grabber = (DirectShow.ISampleGrabber)grabber;
            _ = i_grabber.SetBufferSamples(true);

            DirectShow.IBaseFilter renderer = DirectShow.CoCreateInstance(DirectShow.DsGuid.CLSID_NullRenderer) as DirectShow.IBaseFilter;
            _ = graph.AddFilter(renderer, "NullRenderer");

            try
            {
                Guid mediaType = DirectShow.DsGuid.MEDIATYPE_Video;
                _ = builder.RenderStream(ref pinCategory, ref mediaType, vcap_source, grabber, renderer);
            }
            catch (Exception)
            {
                return null;
            }

            DirectShow.AM_MEDIA_TYPE mt = new DirectShow.AM_MEDIA_TYPE();
            _ = i_grabber.GetConnectedMediaType(mt);

            DirectShow.VIDEOINFOHEADER header = (DirectShow.VIDEOINFOHEADER)Marshal.PtrToStructure(mt.pbFormat, typeof(DirectShow.VIDEOINFOHEADER));
            int width = header.bmiHeader.biWidth;
            int height = header.bmiHeader.biHeight;
            int stride = width * (header.bmiHeader.biBitCount / 8);
            DirectShow.DeleteMediaType(ref mt);

            return new SampleGrabberInfo() { Grabber = i_grabber, Width = width, Height = height, Stride = stride };
        }

        private DirectShow.IBaseFilter CreateSampleGrabber()
        {
            DirectShow.IBaseFilter filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_SampleGrabber);
            DirectShow.ISampleGrabber ismp = filter as DirectShow.ISampleGrabber;
            DirectShow.AM_MEDIA_TYPE mt = new DirectShow.AM_MEDIA_TYPE();
            mt.MajorType = DirectShow.DsGuid.MEDIATYPE_Video;
            mt.SubType = DirectShow.DsGuid.MEDIASUBTYPE_RGB24;
            _ = ismp.SetMediaType(mt);
            return filter;
        }

        private DirectShow.IBaseFilter CreateVideoCaptureSource(int index, VideoFormat format)
        {
            DirectShow.IBaseFilter filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory, index);
            DirectShow.IPin pin = DirectShow.FindPin(filter, 0, DirectShow.PIN_DIRECTION.PINDIR_OUTPUT);
            SetVideoOutputFormat(pin, format);
            return filter;
        }

        private static void SetVideoOutputFormat(DirectShow.IPin pin, VideoFormat format)
        {
            VideoFormat[] formats = GetVideoOutputFormat(pin);

            for (int i = 0; i < formats.Length; i++)
            {
                VideoFormat item = formats[i];

                if (item.MajorType != DirectShow.DsGuid.GetNickname(DirectShow.DsGuid.MEDIATYPE_Video))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType)
                {
                    continue;
                }

                if (item.Caps.Guid != DirectShow.DsGuid.FORMAT_VideoInfo)
                {
                    continue;
                }

                if (item.Size.Width == format.Size.Width && item.Size.Height == format.Size.Height)
                {
                    SetVideoOutputFormat(pin, i, format.Size, format.TimePerFrame);
                    return;
                }
            }

            for (int i = 0; i < formats.Length; i++)
            {
                VideoFormat item = formats[i];

                if (item.MajorType != DirectShow.DsGuid.GetNickname(DirectShow.DsGuid.MEDIATYPE_Video))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType)
                {
                    continue;
                }

                if (item.Caps.Guid != DirectShow.DsGuid.FORMAT_VideoInfo)
                {
                    continue;
                }

                if (item.Caps.OutputGranularityX == 0)
                {
                    continue;
                }

                if (item.Caps.OutputGranularityY == 0)
                {
                    continue;
                }

                for (int w = item.Caps.MinOutputSize.cx; w < item.Caps.MaxOutputSize.cx; w += item.Caps.OutputGranularityX)
                {
                    for (int h = item.Caps.MinOutputSize.cy; h < item.Caps.MaxOutputSize.cy; h += item.Caps.OutputGranularityY)
                    {
                        if (w == format.Size.Width && h == format.Size.Height)
                        {
                            SetVideoOutputFormat(pin, i, format.Size, format.TimePerFrame);
                            return;
                        }
                    }
                }
            }

            SetVideoOutputFormat(pin, 0, Size.Empty, 0);
        }

        private static VideoFormat[] GetVideoOutputFormat(DirectShow.IPin pin)
        {
            DirectShow.IAMStreamConfig config = pin as DirectShow.IAMStreamConfig;
            if (config == null)
            {
                throw new InvalidOperationException("no IAMStreamConfig interface.");
            }

            int cap_count = 0, cap_size = 0;
            _ = config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
            if (cap_size != Marshal.SizeOf(typeof(DirectShow.VIDEO_STREAM_CONFIG_CAPS)))
            {
                throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
            }

            VideoFormat[] result = new VideoFormat[cap_count];

            IntPtr cap_data = Marshal.AllocHGlobal(cap_size);

            for (int i = 0; i < cap_count; i++)
            {
                VideoFormat entry = new VideoFormat();

                DirectShow.AM_MEDIA_TYPE mt = null;
                config.GetStreamCaps(i, ref mt, cap_data);
                entry.Caps = PtrToStructure<DirectShow.VIDEO_STREAM_CONFIG_CAPS>(cap_data);

                entry.MajorType = DirectShow.DsGuid.GetNickname(mt.MajorType);
                entry.SubType = DirectShow.DsGuid.GetNickname(mt.SubType);

                if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo)
                {
                    DirectShow.VIDEOINFOHEADER vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER>(mt.pbFormat);
                    entry.Size = new Size(vinfo.bmiHeader.biWidth, vinfo.bmiHeader.biHeight);
                    entry.TimePerFrame = vinfo.AvgTimePerFrame;
                }
                else if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo2)
                {
                    DirectShow.VIDEOINFOHEADER2 vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER2>(mt.pbFormat);
                    entry.Size = new Size(vinfo.bmiHeader.biWidth, vinfo.bmiHeader.biHeight);
                    entry.TimePerFrame = vinfo.AvgTimePerFrame;
                }

                DirectShow.DeleteMediaType(ref mt);

                result[i] = entry;
            }

            Marshal.FreeHGlobal(cap_data);

            return result;
        }

        private static void SetVideoOutputFormat(DirectShow.IPin pin, int index, Size size, long timePerFrame)
        {
            if (!(pin is DirectShow.IAMStreamConfig config))
            {
                throw new InvalidOperationException("no IAMStreamConfig interface.");
            }

            int cap_count = 0, cap_size = 0;
            _ = config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
            if (cap_size != Marshal.SizeOf(typeof(DirectShow.VIDEO_STREAM_CONFIG_CAPS)))
            {
                throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
            }

            IntPtr cap_data = Marshal.AllocHGlobal(cap_size);

            DirectShow.AM_MEDIA_TYPE mt = null;
            _ = config.GetStreamCaps(index, ref mt, cap_data);
            _ = PtrToStructure<DirectShow.VIDEO_STREAM_CONFIG_CAPS>(cap_data);

            if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo)
            {
                DirectShow.VIDEOINFOHEADER vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER>(mt.pbFormat);
                if (!size.IsEmpty) { vinfo.bmiHeader.biWidth = (int)size.Width; vinfo.bmiHeader.biHeight = (int)size.Height; }
                if (timePerFrame > 0) { vinfo.AvgTimePerFrame = timePerFrame; }
                Marshal.StructureToPtr(vinfo, mt.pbFormat, true);
            }
            else if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo2)
            {
                DirectShow.VIDEOINFOHEADER2 vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER2>(mt.pbFormat);
                if (!size.IsEmpty) { vinfo.bmiHeader.biWidth = (int)size.Width; vinfo.bmiHeader.biHeight = (int)size.Height; }
                if (timePerFrame > 0) { vinfo.AvgTimePerFrame = timePerFrame; }
                Marshal.StructureToPtr(vinfo, mt.pbFormat, true);
            }

            _ = config.SetFormat(mt);

            if (cap_data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(cap_data);
            }

            if (mt != null)
            {
                DirectShow.DeleteMediaType(ref mt);
            }
        }

        private static T PtrToStructure<T>(IntPtr ptr)
        {
            return (T)Marshal.PtrToStructure(ptr, typeof(T));
        }

        private class SampleGrabberCallback : DirectShow.ISampleGrabberCB
        {
            private byte[] buffer = null;
            private Bitmap bmp;

            public delegate void Event(Bitmap bmp);
            public event Event Buffered;

            public delegate void Event2(SampleGrabberCallback sample, string description);
            public event Event2 Error;
            public event Event2 Finished;

            private readonly BitmapBuilder BmpBuilder;
            private string FinishType, MonikerString;
            private int BufferLength = 0;
            private bool OnWork = false;

            public SampleGrabberCallback(DirectShow.ISampleGrabber grabber, int width, int height, int stride, bool useCache)
            {
                BmpBuilder = new BitmapBuilder(width, height, stride, useCache);
                _ = grabber.SetCallback(this, 1);

                ManagementEventWatcher watcher = new ManagementEventWatcher();
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2 or EventType = 3");
                watcher.Query = query;
                watcher.EventArrived += Watcher_EventArrived;
                watcher.Start();
            }

            public void SetStart(string monikerString)
            {
                if (OnWork == false)
                {
                    MonikerString = monikerString;
                    OnWork = true;
                    StillCheck();
                }
            }

            public void SetStop()
            {
                OnWork = false;
            }

            private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
            {
                _ = FindDevices();
            }

            private async void StillCheck()
            {
                try
                {
                    await Task.Delay(1000);
                    if (buffer != null)
                    {
                        do
                        {
                            await Task.Delay(8);
                            if (deviceLists[MonikerString] == -1)
                            {
                                FinishType = "DeviceLost";
                                SetStop();
                            }
                            else
                            {
                                Buffered?.Invoke(GetBitmap());
                            }
                        }
                        while (OnWork);
                    }
                    else
                    {
                        FinishType = "No Signal";
                        SetStop();
                    }
                }
                catch (Exception ex)
                {
                    Error?.Invoke(this, ex.Message);
                    FinishType = "VideoSourceError";
                    SetStop();
                }
                finally
                {
                    buffer = null;
                    bmp = null;
                    Finished?.Invoke(this, FinishType ?? "StoppedByUser");
                }
            }

            public Bitmap GetBitmap()
            {
                lock (buffer)
                {
                    bmp = BmpBuilder.BufferToBitmap(buffer);
                    bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);

                    return bmp;
                }
            }

            public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
            {
                try
                {
                    BufferLength = BufferLen;
                    buffer = new byte[BufferLength];
                    Marshal.Copy(pBuffer, buffer, 0, BufferLen);
                }
                catch (Exception ex)
                {
                    Error?.Invoke(this, ex.Message);
                    FinishType = "VideoSourceError";
                    SetStop();
                }

                return 0;
            }

            public int SampleCB(double SampleTime, DirectShow.IMediaSample pSample)
            {
                return 0;
            }
        }

        private class BitmapBuilder
        {
            private readonly int Width, Height, Stride;

            public BitmapBuilder(int width, int height, int stride, bool dummy)
            {
                Width = width;
                Height = height;
                Stride = stride;

                EmptyBitmap = new Bitmap(width, height);
            }

            public Bitmap BufferToBitmap(byte[] buffer)
            {
                Bitmap result = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                System.Drawing.Imaging.BitmapData bmp_data = result.LockBits(new Rectangle(Point.Empty, result.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                for (int y = 0; y < Height; y++)
                {
                    int src_idx = buffer.Length - (Stride * (y + 1));
                    IntPtr dst = IntPtr.Add(bmp_data.Scan0, Stride * y);
                    Marshal.Copy(buffer, src_idx, dst, Stride);
                }

                result.UnlockBits(bmp_data);

                return result ?? EmptyBitmap;
            }

            public static Bitmap EmptyBitmap { get; private set; }
        }

        private class VideoFormat
        {
            public string MajorType { get; set; }
            public string SubType { get; set; }
            public Size Size { get; set; }
            public long TimePerFrame { get; set; }

            public DirectShow.VIDEO_STREAM_CONFIG_CAPS Caps { get; set; }

            public override string ToString()
            {
                return string.Format("{0}, {1}, {2}, {3}, {4}", MajorType, SubType, Size, TimePerFrame, CapsString());
            }

            private string CapsString()
            {
                StringBuilder sb = new StringBuilder();
                _ = sb.AppendFormat("{0}, ", DirectShow.DsGuid.GetNickname(Caps.Guid));
                foreach (System.Reflection.FieldInfo info in Caps.GetType().GetFields())
                {
                    _ = sb.AppendFormat("{0}={1}, ", info.Name, info.GetValue(Caps));
                }
                return sb.ToString();
            }
        }

        private class SampleGrabberInfo
        {
            public DirectShow.ISampleGrabber Grabber { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Stride { get; set; }
        }

        private static class DirectShow
        {
            public static object CoCreateInstance(Guid clsid)
            {
                return Activator.CreateInstance(Type.GetTypeFromCLSID(clsid));
            }

            public static void ReleaseInstance<T>(ref T com) where T : class
            {
                if (com != null)
                {
                    _ = Marshal.ReleaseComObject(com);
                    com = null;
                }
            }

            public static IGraphBuilder CreateGraph()
            {
                return CoCreateInstance(DsGuid.CLSID_FilterGraph) as IGraphBuilder;
            }

            public static void PlayGraph(IGraphBuilder graph, FILTER_STATE state)
            {
                if (!(graph is IMediaControl mediaControl))
                {
                    return;
                }

                switch (state)
                {
                    case FILTER_STATE.Paused: _ = mediaControl.Pause(); break;
                    case FILTER_STATE.Stopped: _ = mediaControl.Stop(); break;
                    default: _ = mediaControl.Run(); break;
                }
            }

            public static List<string> GetFiltes(Guid category)
            {
                List<string> result = new List<string>();

                EnumMonikers(category, (moniker, prop) =>
                {
                    object value = null;
                    _ = prop.Read("FriendlyName", ref value, 0);
                    string name = (string)value;

                    result.Add(name);

                    return false;
                });

                return result;
            }

            public static IBaseFilter CreateFilter(Guid clsid)
            {
                return CoCreateInstance(clsid) as IBaseFilter;
            }

            public static IBaseFilter CreateFilter(Guid category, int index)
            {
                IBaseFilter result = null;

                int curr_index = 0;
                EnumMonikers(category, (moniker, prop) =>
                {
                    if (index != curr_index++)
                    {
                        return false;
                    }

                    Guid guid = DsGuid.IID_IBaseFilter;
                    moniker.BindToObject(null, null, ref guid, out object value);
                    result = value as IBaseFilter;
                    return true;
                });

                return result ?? throw new ArgumentException("can't create filter.");
            }

            private static void EnumMonikers(Guid category, Func<IMoniker, IPropertyBag, bool> func)
            {
                IEnumMoniker enumerator = null;
                ICreateDevEnum device = null;

                try
                {
                    device = (ICreateDevEnum)Activator.CreateInstance(Type.GetTypeFromCLSID(DsGuid.CLSID_SystemDeviceEnum));
                    _ = device.CreateClassEnumerator(ref category, ref enumerator, 0);
                    if (enumerator == null)
                    {
                        return;
                    }

                    IMoniker[] monikers = new IMoniker[1];
                    IntPtr fetched = IntPtr.Zero;

                    while (enumerator.Next(monikers.Length, monikers, fetched) == 0)
                    {
                        IMoniker moniker = monikers[0];
                        Guid guid = DsGuid.IID_IPropertyBag;
                        moniker.BindToStorage(null, null, ref guid, out object value);
                        IPropertyBag prop = (IPropertyBag)value;

                        try
                        {
                            bool rc = func(moniker, prop);
                            if (rc)
                            {
                                break;
                            }
                        }
                        finally
                        {
                            _ = Marshal.ReleaseComObject(prop);
                            if (moniker != null)
                            {
                                _ = Marshal.ReleaseComObject(moniker);
                            }
                        }
                    }
                }
                finally
                {
                    if (enumerator != null)
                    {
                        _ = Marshal.ReleaseComObject(enumerator);
                    }

                    if (device != null)
                    {
                        _ = Marshal.ReleaseComObject(device);
                    }
                }
            }

            public static IPin FindPin(IBaseFilter filter, string name)
            {
                IPin result = EnumPins(filter, (pin, info) =>
                {
                    return info.achName == name;
                });

                return result ?? throw new ArgumentException("can't fild pin.");
            }

            public static IPin FindPin(IBaseFilter filter, int index, PIN_DIRECTION direction)
            {
                int curr_index = 0;
                IPin result = EnumPins(filter, (pin, info) =>
                {
                    if (info.dir != direction) return false;
                    return index == curr_index++;
                });

                return result ?? throw new ArgumentException("can't fild pin.");
            }

            public static IPin FindPin(IBaseFilter filter, int index, PIN_DIRECTION direction, Guid category)
            {
                int curr_index = 0;
                IPin result = EnumPins(filter, (pin, info) =>
                {
                    if (info.dir != direction)
                    {
                        return false;
                    }

                    if (GetPinCategory(pin) != category)
                    {
                        return false;
                    }

                    return index == curr_index++;
                });

                return result ?? throw new ArgumentException("can't fild pin.");
            }

            private static Guid GetPinCategory(IPin pPin)
            {
                if (!(pPin is IKsPropertySet kps))
                {
                    return Guid.Empty;
                }

                int size = Marshal.SizeOf(typeof(Guid));
                IntPtr ptr = Marshal.AllocCoTaskMem(size);

                try
                {
                    int hr = kps.Get(DsGuid.AMPROPSETID_PIN, (int)AMPropertyPin.Category, IntPtr.Zero, 0, ptr, size, out int cbBytes);
                    if (hr < 0)
                    {
                        return Guid.Empty;
                    }

                    return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
                }
                finally
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }

            private static IPin EnumPins(IBaseFilter filter, Func<IPin, PIN_INFO, bool> func)
            {
                IEnumPins pins = null;
                IPin ipin = null;

                try
                {
                    _ = filter.EnumPins(ref pins);

                    int fetched = 0;
                    while (pins.Next(1, ref ipin, ref fetched) == 0)
                    {
                        if (fetched == 0)
                        {
                            break;
                        }

                        PIN_INFO info = new PIN_INFO();
                        try
                        {
                            _ = ipin.QueryPinInfo(info);
                            bool rc = func(ipin, info);
                            if (rc)
                            {
                                return ipin;
                            }
                        }
                        finally
                        {
                            if (info.pFilter != null)
                            {
                                _ = Marshal.ReleaseComObject(info.pFilter);
                            }
                        }
                    }
                }
                catch
                {
                    if (ipin != null)
                    {
                        _ = Marshal.ReleaseComObject(ipin);
                    }

                    throw;
                }
                finally
                {
                    if (pins != null)
                    {
                        Marshal.ReleaseComObject(pins);
                    }
                }

                return null;
            }

            public static void ConnectFilter(IGraphBuilder graph, IBaseFilter out_flt, int out_no, IBaseFilter in_flt, int in_no)
            {
                IPin out_pin = FindPin(out_flt, out_no, PIN_DIRECTION.PINDIR_OUTPUT);
                IPin inp_pin = FindPin(in_flt, in_no, PIN_DIRECTION.PINDIR_INPUT);
                _ = graph.Connect(out_pin, inp_pin);
            }

            public static void DeleteMediaType(ref AM_MEDIA_TYPE mt)
            {
                if (mt.lSampleSize != 0)
                {
                    Marshal.FreeCoTaskMem(mt.pbFormat);
                }

                if (mt.pUnk != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(mt.pUnk);
                }

                mt = null;
            }

            [ComVisible(true), ComImport(), Guid("56a8689f-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IFilterGraph
            {
                int AddFilter([In] IBaseFilter pFilter, [In, MarshalAs(UnmanagedType.LPWStr)] string pName);
                int RemoveFilter([In] IBaseFilter pFilter);
                int EnumFilters([In, Out] ref IEnumFilters ppEnum);
                int FindFilterByName([In, MarshalAs(UnmanagedType.LPWStr)] string pName, [In, Out] ref IBaseFilter ppFilter);
                int ConnectDirect([In] IPin ppinOut, [In] IPin ppinIn, [In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int Reconnect([In] IPin ppin);
                int Disconnect([In] IPin ppin);
                int SetDefaultSyncSource();
            }

            [ComVisible(true), ComImport(), Guid("56a868a9-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IGraphBuilder : IFilterGraph
            {
                int Connect([In] IPin ppinOut, [In] IPin ppinIn);
                int Render([In] IPin ppinOut);
                int RenderFile([In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFile, [In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrPlayList);
                int AddSourceFilter([In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFileName, [In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFilterName, [In, Out] ref IBaseFilter ppFilter);
                int SetLogFile(IntPtr hFile);
                int Abort();
                int ShouldOperationContinue();
            }

            [ComVisible(true), ComImport(), Guid("56a868b1-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
            public interface IMediaControl
            {
                int Run();
                int Pause();
                int Stop();
                int GetState(int msTimeout, out int pfs);
                int RenderFile(string strFilename);
                int AddSourceFilter([In] string strFilename, [In, Out, MarshalAs(UnmanagedType.IDispatch)] ref object ppUnk);
                int get_FilterCollection([In, Out, MarshalAs(UnmanagedType.IDispatch)] ref object ppUnk);
                int get_RegFilterCollection([In, Out, MarshalAs(UnmanagedType.IDispatch)] ref object ppUnk);
                int StopWhenReady();
            }

            [ComVisible(true), ComImport(), Guid("93E5A4E0-2D50-11d2-ABFA-00A0C9C6E38D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface ICaptureGraphBuilder2
            {
                int SetFiltergraph([In] IGraphBuilder pfg);
                int GetFiltergraph([In, Out] ref IGraphBuilder ppfg);
                int SetOutputFileName([In] ref Guid pType, [In, MarshalAs(UnmanagedType.LPWStr)] string lpstrFile, [In, Out] ref IBaseFilter ppbf, [In, Out] ref IFileSinkFilter ppSink);
                int FindInterface([In] ref Guid pCategory, [In] ref Guid pType, [In] IBaseFilter pbf, [In] IntPtr riid, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object ppint);
                int RenderStream([In] ref Guid pCategory, [In] ref Guid pType, [In, MarshalAs(UnmanagedType.IUnknown)] object pSource, [In] IBaseFilter pfCompressor, [In] IBaseFilter pfRenderer);
                int ControlStream([In] ref Guid pCategory, [In] ref Guid pType, [In] IBaseFilter pFilter, [In] IntPtr pstart, [In] IntPtr pstop, [In] short wStartCookie, [In] short wStopCookie);
                int AllocCapFile([In, MarshalAs(UnmanagedType.LPWStr)] string lpstrFile, [In] long dwlSize);
                int CopyCaptureFile([In, MarshalAs(UnmanagedType.LPWStr)] string lpwstrOld, [In, MarshalAs(UnmanagedType.LPWStr)] string lpwstrNew, [In] int fAllowEscAbort, [In] IAMCopyCaptureFileProgress pFilter);
                int FindPin([In] object pSource, [In] int pindir, [In] ref Guid pCategory, [In] ref Guid pType, [In, MarshalAs(UnmanagedType.Bool)] bool fUnconnected, [In] int num, [Out] out IntPtr ppPin);
            }

            [ComVisible(true), ComImport(), Guid("a2104830-7c70-11cf-8bce-00aa00a3f1a6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IFileSinkFilter
            {
                int SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int GetCurFile([In, Out, MarshalAs(UnmanagedType.LPWStr)] ref string pszFileName, [Out, MarshalAs(UnmanagedType.LPStruct)] out AM_MEDIA_TYPE pmt);
            }

            [ComVisible(true), ComImport(), Guid("670d1d20-a068-11d0-b3f0-00aa003761c5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAMCopyCaptureFileProgress
            {
                int Progress(int iProgress);
            }

            [ComVisible(true), ComImport(), Guid("C6E13370-30AC-11d0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAMCameraControl
            {
                int GetRange([In] CameraControlProperty Property, [In, Out] ref int pMin, [In, Out] ref int pMax, [In, Out] ref int pSteppingDelta, [In, Out] ref int pDefault, [In, Out] ref int pCapsFlag);
                int Set([In] CameraControlProperty Property, [In] int lValue, [In] int Flags);
                int Get([In] CameraControlProperty Property, [In, Out] ref int lValue, [In, Out] ref int Flags);
            }

            [ComVisible(true), ComImport(), Guid("C6E13360-30AC-11d0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAMVideoProcAmp
            {
                int GetRange([In] VideoProcAmpProperty Property, [In, Out] ref int pMin, [In, Out] ref int pMax, [In, Out] ref int pSteppingDelta, [In, Out] ref int pDefault, [In, Out] ref int pCapsFlag);
                int Set([In] VideoProcAmpProperty Property, [In] int lValue, [In] int Flags);
                int Get([In] VideoProcAmpProperty Property, [In, Out] ref int lValue, [In, Out] ref int Flags);
            }

            [ComVisible(true), ComImport(), Guid("6A2E0670-28E4-11D0-A18C-00A0C9118956"), System.Security.SuppressUnmanagedCodeSecurity, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAMVideoControl
            {
                int GetCaps([In] IPin pPin, [Out] out VideoControlFlags pCapsFlags);
                int SetMode([In] IPin pPin, [In] VideoControlFlags Mode);
                int GetMode([In] IPin pPin, [Out] out VideoControlFlags Mode);
                int GetCurrentActualFrameRate([In] IPin pPin, [Out] out long ActualFrameRate);
                int GetMaxAvailableFrameRate([In] IPin pPin, [In] int iIndex, [In] Size Dimensions, [Out] out long MaxAvailableFrameRate);
                int GetFrameRateList([In] IPin pPin, [In] int iIndex, [In] Size Dimensions, [Out] out int ListSize, [Out] out IntPtr FrameRates);
            }

            [ComVisible(true), ComImport(), Guid("31EFAC30-515C-11d0-A9AA-00AA0061BE93"), System.Security.SuppressUnmanagedCodeSecurity, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IKsPropertySet
            {
                [PreserveSig]
                int Set([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidPropSet, [In] int dwPropID, [In] IntPtr pInstanceData, [In] int cbInstanceData, [In] IntPtr pPropData, [In] int cbPropData);

                [PreserveSig]
                int Get([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidPropSet, [In] int dwPropID, [In] IntPtr pInstanceData, [In] int cbInstanceData, [In, Out] IntPtr pPropData, [In] int cbPropData, [Out] out int pcbReturned);

                [PreserveSig]
                int QuerySupported([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidPropSet, [In] int dwPropID, [Out] out int pTypeSupport);
            }

            [ComVisible(true), ComImport(), Guid("56a86895-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IBaseFilter
            {
                int GetClassID([Out] out Guid pClassID);
                int Stop();
                int Pause();
                int Run(long tStart);
                int GetState(int dwMilliSecsTimeout, [In, Out] ref int filtState);
                int SetSyncSource([In] IReferenceClock pClock);
                int GetSyncSource([In, Out] ref IReferenceClock pClock);
                int EnumPins([In, Out] ref IEnumPins ppEnum);
                int FindPin([In, MarshalAs(UnmanagedType.LPWStr)] string Id, [In, Out] ref IPin ppPin);
                int QueryFilterInfo([Out] FILTER_INFO pInfo);
                int JoinFilterGraph([In] IFilterGraph pGraph, [In, MarshalAs(UnmanagedType.LPWStr)] string pName);
                int QueryVendorInfo([In, Out, MarshalAs(UnmanagedType.LPWStr)] ref string pVendorInfo);
            }

            [ComVisible(true), ComImport(), Guid("56a86893-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IEnumFilters
            {
                int Next([In] int cFilters, [In, Out] ref IBaseFilter ppFilter, [In, Out] ref int pcFetched);
                int Skip([In] int cFilters);
                void Reset();
                void Clone([In, Out] ref IEnumFilters ppEnum);
            }

            [ComVisible(true), ComImport(), Guid("C6E13340-30AC-11d0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAMStreamConfig
            {
                int SetFormat([In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int GetFormat([In, Out, MarshalAs(UnmanagedType.LPStruct)] ref AM_MEDIA_TYPE ppmt);
                int GetNumberOfCapabilities(ref int piCount, ref int piSize);
                int GetStreamCaps(int iIndex, [In, Out, MarshalAs(UnmanagedType.LPStruct)] ref AM_MEDIA_TYPE ppmt, IntPtr pSCC);
            }

            [ComVisible(true), ComImport(), Guid("56a8689a-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IMediaSample
            {
                int GetPointer(ref IntPtr ppBuffer);
                int GetSize();
                int GetTime(ref long pTimeStart, ref long pTimeEnd);
                int SetTime([In, MarshalAs(UnmanagedType.LPStruct)] UInt64 pTimeStart, [In, MarshalAs(UnmanagedType.LPStruct)] UInt64 pTimeEnd);
                int IsSyncPoint();
                int SetSyncPoint([In, MarshalAs(UnmanagedType.Bool)] bool bIsSyncPoint);
                int IsPreroll();
                int SetPreroll([In, MarshalAs(UnmanagedType.Bool)] bool bIsPreroll);
                int GetActualDataLength();
                int SetActualDataLength(int len);
                int GetMediaType([In, Out, MarshalAs(UnmanagedType.LPStruct)] ref AM_MEDIA_TYPE ppMediaType);
                int SetMediaType([In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pMediaType);
                int IsDiscontinuity();
                int SetDiscontinuity([In, MarshalAs(UnmanagedType.Bool)] bool bDiscontinuity);
                int GetMediaTime(ref long pTimeStart, ref long pTimeEnd);
                int SetMediaTime([In, MarshalAs(UnmanagedType.LPStruct)] UInt64 pTimeStart, [In, MarshalAs(UnmanagedType.LPStruct)] UInt64 pTimeEnd);
            }

            [ComVisible(true), ComImport(), Guid("89c31040-846b-11ce-97d3-00aa0055595a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IEnumMediaTypes
            {
                int Next([In] int cMediaTypes, [In, Out, MarshalAs(UnmanagedType.LPStruct)] ref AM_MEDIA_TYPE ppMediaTypes, [In, Out] ref int pcFetched);
                int Skip([In] int cMediaTypes);
                int Reset();
                int Clone([In, Out] ref IEnumMediaTypes ppEnum);
            }

            [ComVisible(true), ComImport(), Guid("56a86891-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IPin
            {
                int Connect([In] IPin pReceivePin, [In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int ReceiveConnection([In] IPin pReceivePin, [In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int Disconnect();
                int ConnectedTo([In, Out] ref IPin ppPin);
                int ConnectionMediaType([Out, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int QueryPinInfo([Out] PIN_INFO pInfo);
                int QueryDirection(ref PIN_DIRECTION pPinDir);
                int QueryId([In, Out, MarshalAs(UnmanagedType.LPWStr)] ref string Id);
                int QueryAccept([In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int EnumMediaTypes([In, Out] ref IEnumMediaTypes ppEnum);
                int QueryInternalConnections(IntPtr apPin, [In, Out] ref int nPin);
                int EndOfStream();
                int BeginFlush();
                int EndFlush();
                int NewSegment(long tStart, long tStop, double dRate);
            }

            [ComVisible(true), ComImport(), Guid("56a86892-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IEnumPins
            {
                int Next([In] int cPins, [In, Out] ref IPin ppPins, [In, Out] ref int pcFetched);
                int Skip([In] int cPins);
                void Reset();
                void Clone([In, Out] ref IEnumPins ppEnum);
            }

            [ComVisible(true), ComImport(), Guid("56a86897-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IReferenceClock
            {
                int GetTime(ref long pTime);
                int AdviseTime(long baseTime, long streamTime, IntPtr hEvent, ref int pdwAdviseCookie);
                int AdvisePeriodic(long startTime, long periodTime, IntPtr hSemaphore, ref int pdwAdviseCookie);
                int Unadvise(int dwAdviseCookie);
            }

            [ComVisible(true), ComImport(), Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface ICreateDevEnum
            {
                int CreateClassEnumerator([In] ref Guid pType, [In, Out] ref IEnumMoniker ppEnumMoniker, [In] int dwFlags);
            }

            [ComVisible(true), ComImport(), Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IPropertyBag
            {
                int Read([MarshalAs(UnmanagedType.LPWStr)] string PropName, ref object Var, int ErrorLog);
                int Write(string PropName, ref object Var);
            }

            [ComVisible(true), ComImport(), Guid("6B652FFF-11FE-4fce-92AD-0266B5D7C78F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface ISampleGrabber
            {
                int SetOneShot([In, MarshalAs(UnmanagedType.Bool)] bool OneShot);
                int SetMediaType([In, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int GetConnectedMediaType([Out, MarshalAs(UnmanagedType.LPStruct)] AM_MEDIA_TYPE pmt);
                int SetBufferSamples([In, MarshalAs(UnmanagedType.Bool)] bool BufferThem);
                int GetCurrentBuffer(ref int pBufferSize, IntPtr pBuffer);
                int GetCurrentSample(IntPtr ppSample);
                int SetCallback(ISampleGrabberCB pCallback, int WhichMethodToCallback);
            }

            [ComVisible(true), ComImport(), Guid("0579154A-2B53-4994-B0D0-E773148EFF85"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface ISampleGrabberCB
            {
                [PreserveSig()]
                int SampleCB(double SampleTime, IMediaSample pSample);

                [PreserveSig()]
                int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen);
            }

            [ComVisible(true), ComImport(), Guid("56a868b4-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
            public interface IVideoWindow
            {
                int put_Caption(string caption);
                int get_Caption([In, Out] ref string caption);
                int put_WindowStyle(int windowStyle);
                int get_WindowStyle(ref int windowStyle);
                int put_WindowStyleEx(int windowStyleEx);
                int get_WindowStyleEx(ref int windowStyleEx);
                int put_AutoShow(int autoShow);
                int get_AutoShow(ref int autoShow);
                int put_WindowState(int windowState);
                int get_WindowState(ref int windowState);
                int put_BackgroundPalette(int backgroundPalette);
                int get_BackgroundPalette(ref int backgroundPalette);
                int put_Visible(int visible);
                int get_Visible(ref int visible);
                int put_Left(int left);
                int get_Left(ref int left);
                int put_Width(int width);
                int get_Width(ref int width);
                int put_Top(int top);
                int get_Top(ref int top);
                int put_Height(int height);
                int get_Height(ref int height);
                int put_Owner(IntPtr owner);
                int get_Owner(ref IntPtr owner);
                int put_MessageDrain(IntPtr drain);
                int get_MessageDrain(ref IntPtr drain);
                int get_BorderColor(ref int color);
                int put_BorderColor(int color);
                int get_FullScreenMode(ref int fullScreenMode);
                int put_FullScreenMode(int fullScreenMode);
                int SetWindowForeground(int focus);
                int NotifyOwnerMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
                int SetWindowPosition(int left, int top, int width, int height);
                int GetWindowPosition(ref int left, ref int top, ref int width, ref int height);
                int GetMinIdealImageSize(ref int width, ref int height);
                int GetMaxIdealImageSize(ref int width, ref int height);
                int GetRestorePosition(ref int left, ref int top, ref int width, ref int height);
                int HideCursor(int HideCursorValue);
                int IsCursorHidden(ref int hideCursor);
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential), ComVisible(false)]
            public class AM_MEDIA_TYPE
            {
                public Guid MajorType;
                public Guid SubType;
                [MarshalAs(UnmanagedType.Bool)]
                public bool bFixedSizeSamples;
                [MarshalAs(UnmanagedType.Bool)]
                public bool bTemporalCompression;
                public uint lSampleSize;
                public Guid FormatType;
                public IntPtr pUnk;
                public uint cbFormat;
                public IntPtr pbFormat;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode), ComVisible(false)]
            public class FILTER_INFO
            {
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string achName;
                [MarshalAs(UnmanagedType.IUnknown)]
                public object pGraph;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode), ComVisible(false)]
            public class PIN_INFO
            {
                public IBaseFilter pFilter;
                public PIN_DIRECTION dir;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string achName;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 8), ComVisible(false)]
            public struct VIDEO_STREAM_CONFIG_CAPS
            {
                public Guid Guid;
                public uint VideoStandard;
                public SIZE InputSize;
                public SIZE MinCroppingSize;
                public SIZE MaxCroppingSize;
                public int CropGranularityX;
                public int CropGranularityY;
                public int CropAlignX;
                public int CropAlignY;
                public SIZE MinOutputSize;
                public SIZE MaxOutputSize;
                public int OutputGranularityX;
                public int OutputGranularityY;
                public int StretchTapsX;
                public int StretchTapsY;
                public int ShrinkTapsX;
                public int ShrinkTapsY;
                public long MinFrameInterval;
                public long MaxFrameInterval;
                public int MinBitsPerSecond;
                public int MaxBitsPerSecond;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential), ComVisible(false)]
            public struct VIDEOINFOHEADER
            {
                public RECT SrcRect;
                public RECT TrgRect;
                public int BitRate;
                public int BitErrorRate;
                public long AvgTimePerFrame;
                public BITMAPINFOHEADER bmiHeader;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential), ComVisible(false)]
            public struct VIDEOINFOHEADER2
            {
                public RECT SrcRect;
                public RECT TrgRect;
                public int BitRate;
                public int BitErrorRate;
                public long AvgTimePerFrame;
                public int InterlaceFlags;
                public int CopyProtectFlags;
                public int PictAspectRatioX;
                public int PictAspectRatioY;
                public int ControlFlags; // or Reserved1
                public int Reserved2;
                public BITMAPINFOHEADER bmiHeader;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 2), ComVisible(false)]
            public struct BITMAPINFOHEADER
            {
                public int biSize;
                public int biWidth;
                public int biHeight;
                public short biPlanes;
                public short biBitCount;
                public int biCompression;
                public int biSizeImage;
                public int biXPelsPerMeter;
                public int biYPelsPerMeter;
                public int biClrUsed;
                public int biClrImportant;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential), ComVisible(false)]
            public struct WAVEFORMATEX
            {
                public ushort wFormatTag;
                public ushort nChannels;
                public uint nSamplesPerSec;
                public uint nAvgBytesPerSec;
                public short nBlockAlign;
                public short wBitsPerSample;
                public short cbSize;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 8), ComVisible(false)]
            public struct SIZE
            {
                public int cx;
                public int cy;
                public override string ToString() { return string.Format("{{{0}, {1}}}", cx, cy); } // for debugging.
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential), ComVisible(false)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
                public override string ToString() { return string.Format("{{{0}, {1}, {2}, {3}}}", Left, Top, Right, Bottom); } // for debugging.
            }

            [ComVisible(false)]
            public enum PIN_DIRECTION
            {
                PINDIR_INPUT = 0,
                PINDIR_OUTPUT = 1,
            }

            [ComVisible(false)]
            public enum FILTER_STATE : int
            {
                Stopped = 0,
                Paused = 1,
                Running = 2,
            }

            [ComVisible(false)]
            public enum CameraControlProperty
            {
                Pan = 0,
                Tilt = 1,
                Roll = 2,
                Zoom = 3,
                Exposure = 4,
                Iris = 5,
                Focus = 6,
            }

            [ComVisible(false), Flags()]
            public enum CameraControlFlags
            {
                Auto = 0x0001,
                Manual = 0x0002,
            }

            [ComVisible(false)]
            public enum VideoProcAmpProperty
            {
                Brightness = 0,
                Contrast = 1,
                Hue = 2,
                Saturation = 3,
                Sharpness = 4,
                Gamma = 5,
                ColorEnable = 6,
                WhiteBalance = 7,
                BacklightCompensation = 8,
                Gain = 9
            }

            [ComVisible(false)]
            public enum AMPropertyPin
            {
                Category,
                Medium
            }

            [ComVisible(false), Flags()]
            public enum VideoControlFlags
            {
                FlipHorizontal = 0x01,
                FlipVertical = 0x02,
                ExternalTriggerEnable = 0x04,
                Trigger = 0x08
            }

            public static class DsGuid
            {
                // MediaType
                public static readonly Guid MEDIATYPE_Video = new Guid("{73646976-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIATYPE_Audio = new Guid("{73647561-0000-0010-8000-00AA00389B71}");

                // SubType
                public static readonly Guid MEDIASUBTYPE_None = new Guid("{E436EB8E-524F-11CE-9F53-0020AF0BA770}");
                public static readonly Guid MEDIASUBTYPE_YUYV = new Guid("{56595559-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_IYUV = new Guid("{56555949-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_YVU9 = new Guid("{39555659-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_YUY2 = new Guid("{32595559-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_YVYU = new Guid("{55595659-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_UYVY = new Guid("{59565955-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_MJPG = new Guid("{47504A4D-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_RGB565 = new Guid("{E436EB7B-524F-11CE-9F53-0020AF0BA770}");
                public static readonly Guid MEDIASUBTYPE_RGB555 = new Guid("{E436EB7C-524F-11CE-9F53-0020AF0BA770}");
                public static readonly Guid MEDIASUBTYPE_RGB24 = new Guid("{E436EB7D-524F-11CE-9F53-0020AF0BA770}");
                public static readonly Guid MEDIASUBTYPE_RGB32 = new Guid("{E436EB7E-524F-11CE-9F53-0020AF0BA770}");
                public static readonly Guid MEDIASUBTYPE_ARGB32 = new Guid("{773C9AC0-3274-11D0-B724-00AA006C1A01}");
                public static readonly Guid MEDIASUBTYPE_PCM = new Guid("{00000001-0000-0010-8000-00AA00389B71}");
                public static readonly Guid MEDIASUBTYPE_WAVE = new Guid("{E436EB8B-524F-11CE-9F53-0020AF0BA770}");

                // FormatType
                public static readonly Guid FORMAT_None = new Guid("{0F6417D6-C318-11D0-A43F-00A0C9223196}");
                public static readonly Guid FORMAT_VideoInfo = new Guid("{05589F80-C356-11CE-BF01-00AA0055595A}");
                public static readonly Guid FORMAT_VideoInfo2 = new Guid("{F72A76A0-EB0A-11d0-ACE4-0000C0CC16BA}");
                public static readonly Guid FORMAT_WaveFormatEx = new Guid("{05589F81-C356-11CE-BF01-00AA0055595A}");

                // CLSID
                public static readonly Guid CLSID_AudioInputDeviceCategory = new Guid("{33D9A762-90C8-11d0-BD43-00A0C911CE86}");
                public static readonly Guid CLSID_AudioRendererCategory = new Guid("{E0F158E1-CB04-11d0-BD4E-00A0C911CE86}");
                public static readonly Guid CLSID_VideoInputDeviceCategory = new Guid("{860BB310-5D01-11d0-BD3B-00A0C911CE86}");
                public static readonly Guid CLSID_VideoCompressorCategory = new Guid("{33D9A760-90C8-11d0-BD43-00A0C911CE86}");

                public static readonly Guid CLSID_NullRenderer = new Guid("{C1F400A4-3F08-11D3-9F0B-006008039E37}");
                public static readonly Guid CLSID_SampleGrabber = new Guid("{C1F400A0-3F08-11D3-9F0B-006008039E37}");

                public static readonly Guid CLSID_FilterGraph = new Guid("{E436EBB3-524F-11CE-9F53-0020AF0BA770}");
                public static readonly Guid CLSID_SystemDeviceEnum = new Guid("{62BE5D10-60EB-11d0-BD3B-00A0C911CE86}");
                public static readonly Guid CLSID_CaptureGraphBuilder2 = new Guid("{BF87B6E1-8C27-11d0-B3F0-00AA003761C5}");

                public static readonly Guid IID_IPropertyBag = new Guid("{55272A00-42CB-11CE-8135-00AA004BB851}");
                public static readonly Guid IID_IBaseFilter = new Guid("{56a86895-0ad4-11ce-b03a-0020af0ba770}");
                public static readonly Guid IID_IAMStreamConfig = new Guid("{C6E13340-30AC-11d0-A18C-00A0C9118956}");

                public static readonly Guid PIN_CATEGORY_CAPTURE = new Guid("{fb6c4281-0353-11d1-905f-0000c0cc16ba}");
                public static readonly Guid PIN_CATEGORY_PREVIEW = new Guid("{fb6c4282-0353-11d1-905f-0000c0cc16ba}");
                public static readonly Guid PIN_CATEGORY_STILL = new Guid("{fb6c428a-0353-11d1-905f-0000c0cc16ba}");


                public static readonly Guid AMPROPSETID_PIN = new Guid("9b00f101-1567-11d1-b3f1-00aa003761c5");

                private static Dictionary<Guid, string> NicknameCache = null;

                public static string GetNickname(Guid guid)
                {
                    if (NicknameCache == null)
                    {
                        NicknameCache = typeof(DsGuid).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                            .Where(x => x.FieldType == typeof(Guid))
                            .ToDictionary(x => (Guid)x.GetValue(null), x => x.Name);
                    }

                    if (NicknameCache.ContainsKey(guid))
                    {
                        string name = NicknameCache[guid];
                        string[] elem = name.Split('_');

                        if (elem.Length >= 2)
                        {
                            string text = string.Join("_", elem.Skip(1).ToArray());
                            return string.Format("[{0}]", text);
                        }
                        else
                        {
                            return name;
                        }
                    }

                    return guid.ToString();
                }
            }
        }

        public class FilterInfoCollection
        {
            private readonly List<FilterInfo> filters = new List<FilterInfo>();

            public FilterInfoCollection()
            {
                List<string> FriendName = DirectShow.GetFiltes(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory);
                for (int i = 0; i < FriendName.Count; i++)
                {
                    VideoFormat[] formats = GetVideoFormat(i);
                    filters.Add(new FilterInfo() { Name = FriendName[i], MonikerString = formats[i].ToString() });
                }
            }

            public FilterInfo this[int index] => filters[index];

            public int this[string monikerString] => filters.FindIndex(x => x.MonikerString == monikerString);

            public int Count => filters.Count;
        }

        public class FilterInfo
        {
            public string MonikerString { get; set; }

            public string Name { get; set; }
        }
    }
}
