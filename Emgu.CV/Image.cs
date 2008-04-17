using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using System.IO;
using zlib;
using System.Runtime.Serialization;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;

namespace Emgu.CV
{
    /// <summary>
    /// A wrapper for IplImage
    /// </summary>
    /// <typeparam name="C">ColorType type of this image</typeparam>
    /// <typeparam name="D">Depth of this image (either Byte or Single)</typeparam>
    [Serializable]
    [KnownType(typeof(Point2D<int>))]
    [KnownType(typeof(Rectangle<double>))]
    public class Image<C, D> : Array, ISerializable where C : ColorType, new()
    {
        ///<summary>
        ///Create an Image of random color and one by one in size
        ///</summary>
        public Image()
        {
            _ptr = CvInvoke.cvCreateImage(new MCvSize(1, 1), CvDepth, Color.Dimension);
        }

        /// <summary>
        /// Read image from a file
        /// </summary>
        /// <param name="fileName">the name of the file that contains the image</param>
        public Image(String fileName)
        {
            IntPtr ptr;
            MIplImage mptr;
            if (typeof(C) == typeof(Gray)) //color type is gray
            {
                ptr = CvInvoke.cvLoadImage(fileName, Emgu.CV.CvEnum.LOAD_IMAGE_TYPE.CV_LOAD_IMAGE_GRAYSCALE);
                mptr = (MIplImage)Marshal.PtrToStructure(ptr, typeof(MIplImage));
            }
            else //color type is not gray
            {
                ptr = CvInvoke.cvLoadImage(fileName, CvEnum.LOAD_IMAGE_TYPE.CV_LOAD_IMAGE_COLOR);
                mptr = (MIplImage)Marshal.PtrToStructure(ptr, typeof(MIplImage));

                if (typeof(C) != typeof(Bgr)) //color type is not Bgr
                {
                    IntPtr tmp1 = CvInvoke.cvCreateImage(
                        new MCvSize(mptr.width, mptr.height),
                        (CvEnum.IPL_DEPTH)mptr.depth,
                        3);
                    CvInvoke.cvCvtColor(ptr, tmp1, GetColorCvtCode(new C(), new Bgr()));

                    IntPtr tmp2 = ptr;
                    ptr = tmp1;
                    CvInvoke.cvReleaseImage(ref tmp2);
                    mptr = (MIplImage)Marshal.PtrToStructure(ptr, typeof(MIplImage));
                }
            }

            if (typeof(D) != typeof(Byte)) //depth is not Byte
            {
                IntPtr tmp1 = CvInvoke.cvCreateImage(
                    new MCvSize(mptr.width, mptr.height),
                    CvEnum.IPL_DEPTH.IPL_DEPTH_8U,
                    3);
                CvInvoke.cvConvertScale(ptr, tmp1, 1.0, 0.0);

                IntPtr tmp2 = ptr;
                ptr = tmp1;
                CvInvoke.cvReleaseImage(ref tmp2);
            }

            _ptr = ptr;
        }

        ///<summary>
        ///Create a blank Image of the specified width, height, depth and color.
        ///</summary>
        ///<param name="width">The width of the image</param>
        ///<param name="height">The height of the image</param>
        ///<param name="value">The initial color of the image</param>
        public Image(int width, int height, C value)
            : this(width, height)
        {
            SetValue(value);
        }

        ///<summary>
        ///Create a blank Image of the specified width, height, depth. 
        ///<b>Warning: The pixel of this image contains random values </b>
        ///</summary>
        ///<param name="width">The width of the image</param>
        ///<param name="height">The height of the image</param>
        public Image(int width, int height)
        {
            _ptr = CvInvoke.cvCreateImage(new MCvSize(width, height), CvDepth, Color.Dimension);
        }

        ///<summary>
        ///Create a multi-channel image from multiple gray scale images
        ///</summary>
        ///<param name="channels">The image channels to be merged into a single image</param>
        public Image(Image<Gray, D>[] channels)
        {
            C color = new C();
            int channelCount = color.Dimension;

            Debug.Assert(channelCount == channels.Length);

            _ptr = CvInvoke.cvCreateImage(new MCvSize(channels[0].Width, channels[0].Height), CvDepth, channelCount);

            if (channelCount == 1)
            {
                //if this image only have a single channel
                CvInvoke.cvCopy(channels[0].Ptr, Ptr, IntPtr.Zero);
            }
            else
            {
                for (int i = 0; i < channelCount; )
                {
                    Image<Gray, D> c = channels[i];

                    Debug.Assert(EqualSize(c));

                    CvInvoke.cvSetImageCOI(Ptr, ++i);
                    CvInvoke.cvCopy(c.Ptr, Ptr, IntPtr.Zero);
                }
                CvInvoke.cvSetImageCOI(Ptr, 0);
            }
        }

        #region Implement ISerializable interface
        /// <summary>
        /// Constructor used to deserialize runtime serialized object
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The streaming context</param>
        public Image(SerializationInfo info, StreamingContext context)
        {
            Point2D<int> size = (Point2D<int>)info.GetValue("Size", typeof(Point2D<int>));

            _ptr = CvInvoke.cvCreateImage(new MCvSize(size.X, size.Y), CvDepth, Color.Dimension);
            CompressedBinary = (Byte[])info.GetValue("CompressedBinary", typeof(Byte[]));
            ROI = (Rectangle<double>)info.GetValue("Roi", typeof(Rectangle<double>));
        }

        /// <summary>
        /// A function used for runtime serilization of the object
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">streaming context</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Size", Size);
            info.AddValue("Roi", ROI);
            info.AddValue("CompressedBinary", CompressedBinary);
        }
        #endregion

        #region Image Properties
        ///<summary> 
        /// The type of color for this image
        /// </summary>
        public ColorType Color
        {
            get { return new C(); }
        }

        /// <summary>
        /// The IplImage structure
        /// </summary>
        public MIplImage MIplImage
        {
            get
            {
                return (MIplImage)Marshal.PtrToStructure(Ptr, typeof(MIplImage));
            }
        }

        ///<summary> 
        /// The region of interest for this image 
        ///</summary>
        [XmlElement("RegionOfInterest")]
        //[DataMember]
        public Rectangle<double> ROI
        {
            set
            {
                if (value == null)
                {
                    //reset the image ROI
                    CvInvoke.cvResetImageROI(Ptr);
                }
                else
                {   //set the image ROI to the specific value
                    CvInvoke.cvSetImageROI(Ptr, value.MCvRect);
                }
            }
            get
            {
                //return the image ROI
                return new Rectangle<double>(CvInvoke.cvGetImageROI(Ptr));
            }
        }

        ///<summary> 
        ///The width of the image ( number of pixels in the x direction),
        ///if ROI is set, the width of the ROI 
        ///</summary>
        [XmlIgnore]
        public override int Width { get { return isROISet ? (int)ROI.Width : Marshal.ReadInt32(Ptr, IplImageOffset.width); } }

        ///<summary> 
        ///The height of the image ( number of pixels in the y direction ),
        ///if ROI is set, the height of the ROI 
        ///</summary> 
        [XmlIgnore]
        public override int Height { get { return isROISet ? (int)ROI.Height : Marshal.ReadInt32(Ptr, IplImageOffset.height); } }

        ///<summary> 
        /// The size of the internal iplImage structure, regardness of the ROI of this image: X -- Width; Y -- Height.
        /// When a new size is assigned to this property, the original image is resized (the ROI is resized as well when 
        /// available)
        ///</summary>
        [XmlElement("Size")]
        //[DataMember]
        public Point2D<int> Size
        {
            get
            {
                return new Point2D<int>(
                Marshal.ReadInt32(Ptr, IplImageOffset.width),
                Marshal.ReadInt32(Ptr, IplImageOffset.height));
            }
            set
            {
                Rectangle<double> newRoi = null;
                //Scale the ROI to reflect the change in image size
                if (isROISet)
                {
                    Rectangle<double> roi = ROI;
                    double scaleX = value.X / roi.Width;
                    double scaleY = value.Y / roi.Height;
                    newRoi = new Rectangle<double>((roi.Left * scaleX), (roi.Right * scaleX), (roi.Top * scaleY), (roi.Bottom * scaleY));
                }

                IntPtr img = CvInvoke.cvCreateImage(new MCvSize(value.X, value.Y), CvDepth, Color.Dimension);
                CvInvoke.cvResize(Ptr, img, CvEnum.INTER.CV_INTER_LINEAR);
                CvInvoke.cvReleaseImage(ref _ptr);
                _ptr = img;

                if (newRoi != null)
                    ROI = newRoi;
            }
        }

        /// <summary>
        /// The depth value in opencv for this image
        /// </summary>
        protected CvEnum.IPL_DEPTH CvDepth
        {
            get
            {
                if (typeof(D) == typeof(System.Single))
                    return CvEnum.IPL_DEPTH.IPL_DEPTH_32F;
                else if (typeof(D) == typeof(System.Byte))
                    return CvEnum.IPL_DEPTH.IPL_DEPTH_8U;
                else
                    throw new Emgu.Exception(Emgu.ExceptionHeader.CriticalException, "Unsupported image depth");
            }
        }

        ///<summary> 
        ///Indicates if the region of interest has been set
        ///</summary> 
        public bool isROISet
        {
            get
            {
                return Marshal.ReadIntPtr(Ptr, IplImageOffset.roi) != IntPtr.Zero;
            }
        }

        ///<summary> The average color of this image </summary>
        public C Average
        {
            get
            {
                C res = new C();
                res.CvScalar = CvInvoke.cvAvg(Ptr, IntPtr.Zero);
                return res;
            }
        }

        ///<summary> The sum for each color channel </summary>
        public C Sum
        {
            get
            {
                C res = new C();
                res.CvScalar = CvInvoke.cvSum(Ptr);
                return res;
            }
        }

        ///<summary> 
        ///The binary data of the image regardness of ROI
        ///</summary>
        [XmlIgnore]
        public Byte[] Binary
        {
            get
            {
                MIplImage img = MIplImage;
                int size = img.imageSize;
                Byte[] res = new Byte[size];
                Marshal.Copy(img.imageData, res, 0, size);
                return res;
            }
            set
            {
                MIplImage img = MIplImage;
                Marshal.Copy(value, 0, img.imageData, Math.Min(img.widthStep * img.height, value.Length));
            }
        }

        ///<summary>
        ///The binary data in compressed format regardness of ROI
        ///</summary>
        [XmlElement("CompressedBinary")]
        //[DataMember]
        public Byte[] CompressedBinary
        {
            get
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    //GZipStream  compressedStream = new GZipStream(ms , CompressionMode.Compress);
                    using (zlib.ZOutputStream compressedStream = new zlib.ZOutputStream(ms, zlib.zlibConst.Z_BEST_COMPRESSION))
                    {
                        Byte[] data = Binary;
                        compressedStream.Write(data, 0, data.Length);
                        compressedStream.Flush();
                    }
                    return ms.ToArray();
                }
            }
            set
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (zlib.ZOutputStream compressedStream = new zlib.ZOutputStream(ms))
                    {
                        compressedStream.Write(value, 0, value.Length);
                        Binary = ms.ToArray();
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Set every pixel of the image to the specific color 
        /// </summary>
        /// <param name="color">The color to be set</param>
        public void SetValue(C color)
        {
            CvInvoke.cvSet(Ptr, color.CvScalar, IntPtr.Zero);
        }

        /// <summary>
        /// Set every pixel of the image to the specific color, using a mask
        /// </summary>
        /// <param name="color">The color to be set</param>
        /// <param name="mask">The mask for setting pixels</param>
        public void SetValue(C color, Image<Gray, Byte> mask)
        {
            CvInvoke.cvSet(Ptr, color.CvScalar, mask.Ptr);
        }

        #region Drawing functions
        ///<summary> Draw an Rectangle of the specific color and thickness </summary>
        ///<param name="rect"> The rectangle to be draw</param>
        ///<param name="color"> The color of the rectangle </param>
        ///<param name="thickness"> If thickness is less than 1, the rectangle is filled up </param>
        public void Draw(Rectangle<double> rect, C color, int thickness)
        {
            CvInvoke.cvRectangle(
                Ptr,
                rect.TopLeft.CvPoint,
                rect.BottomRight.CvPoint,
                color.CvScalar,
                (thickness <= 0) ? -1 : thickness,
                CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                0);
        }

        ///<summary> Draw a line segment of the specific color and thickness </summary>
        ///<param name="line"> The line segment to be draw</param>
        ///<param name="color"> The color of the line segment </param>
        ///<param name="thickness"> The thickness of the line segment </param>
        public void Draw(LineSegment2D<int> line, C color, int thickness)
        {
            if (thickness > 0)
                CvInvoke.cvLine(
                    Ptr,
                    line.P1.CvPoint,
                    line.P2.CvPoint,
                    color.CvScalar,
                    thickness,
                    CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                    0);
        }

        ///<summary> Draw a Circle of the specific color and thickness </summary>
        ///<param name="circle"> The circle to be draw</param>
        ///<param name="color"> The color of the circle </param>
        ///<param name="thickness"> If thickness is less than 1, the circle is filled up </param>
        public void Draw<T>(Circle<T> circle, C color, int thickness) where T : IComparable, new()
        {
            CvInvoke.cvCircle(
                Ptr,
                circle.Center.CvPoint,
                System.Convert.ToInt32(circle.Radius),
                color.CvScalar,
                (thickness <= 0) ? -1 : thickness,
                CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                0);
        }

        ///<summary> Draw a Ellipse of the specific color and thickness </summary>
        ///<param name="ellipse"> The ellipse to be draw</param>
        ///<param name="color"> The color of the ellipse </param>
        ///<param name="thickness"> If thickness is less than 1, the ellipse is filled up </param>
        public void Draw(Ellipse<float> ellipse, C color, int thickness)
        {
            CvInvoke.cvEllipse(
                Ptr,
                ellipse.Center.CvPoint,
                new MCvSize(System.Convert.ToInt32(ellipse.Width) >> 1, System.Convert.ToInt32(ellipse.Height) >> 1),
                ellipse.RadianAngle,
                0.0,
                360.0,
                color.CvScalar,
                (thickness <= 0) ? -1 : thickness,
                CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                0);
        }

        /// <summary>
        /// Draw the text using the specific font on the image
        /// </summary>
        /// <param name="message">The text message to be draw</param>
        /// <param name="font">The font used for drawing</param>
        /// <param name="bottomLeft">The location of the bottom left corner of the font</param>
        /// <param name="color">The color of the text</param>
        public void Draw(String message, Font font, Point2D<int> bottomLeft, C color)
        {
            CvInvoke.cvPutText(
                Ptr,
                message,
                bottomLeft.CvPoint,
                font.Ptr,
                color.CvScalar);
        }

        ///<summary> Draw the contour with the specific color and thickness </summary>
        public void Draw(Seq<MCvPoint> c, C external_color, C hole_color, int thickness)
        {
            CvInvoke.cvDrawContours(
                Ptr,
                c.Ptr,
                external_color.CvScalar,
                hole_color.CvScalar,
                0,
                thickness,
                CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                new MCvPoint(0, 0));
        }
        #endregion

        ///<summary>
        ///Erodes <i>this</i> image inplace using a 3x3 rectangular structuring element.
        ///Erosion are applied serveral (iterations) times
        ///</summary>
        public void _Erode(int iterations)
        {
            CvInvoke.cvErode(Ptr, Ptr, IntPtr.Zero, iterations);
        }

        ///<summary>
        ///Dilates <i>this</i> image inplace using a 3x3 rectangular structuring element.
        ///Dilation are applied serveral (iterations) times
        ///</summary>
        public void _Dilate(int iterations)
        {
            CvInvoke.cvDilate(Ptr, Ptr, IntPtr.Zero, iterations);
        }

        ///<summary> Perform Gaussian Smoothing inplace for the current image </summary>
        ///<param name="kernelSize"> The size of the Gaussian kernel (<paramref>kernelSize</paramref> x <paramref>kernelSize</paramref>)</param>
        public void _GaussianSmooth(int kernelSize)
        {
            _GaussianSmooth(kernelSize, 0, 0);
        }

        ///<summary> Perform Gaussian Smoothing inplace for the current image </summary>
        ///<param name="kernelWidth"> The width of the Gaussian kernel</param>
        ///<param name="kernelHeight"> The height of the Gaussian kernel</param>
        ///<param name="sigma"> The standard deviation of the Gaussian kernel</param>
        public void _GaussianSmooth(int kernelWidth, int kernelHeight, double sigma)
        {
            CvInvoke.cvSmooth(Ptr, Ptr, CvEnum.SMOOTH_TYPE.CV_GAUSSIAN, kernelWidth, kernelHeight, sigma, 0);
        }

        /// <summary>
        /// Save this image to the specific file
        /// </summary>
        /// <param name="filename">The name of the file to be saved to</param>
        public void Save(String filename)
        {
            CvInvoke.cvSaveImage(filename, Ptr);
        }

        ///<summary> Sample the pixel values on the specific line segment </summary>
        ///<param name="line"> The line to obtain samples</param>
        public D[] Sample(LineSegment2D<int> line)
        {
            int size = Math.Max(Math.Abs(line.P2.X - line.P1.X), Math.Abs(line.P2.Y - line.P1.Y));
            D[] data = new D[size];
            GCHandle hdata = GCHandle.Alloc(data, GCHandleType.Pinned);
            CvInvoke.cvSampleLine(
                Ptr,
                line.P1.CvPoint,
                line.P2.CvPoint,
                hdata.AddrOfPinnedObject(),
                8);
            hdata.Free();
            return data;
        }

        #region Object Detection
        /// <summary>
        /// Detect HaarCascade object in the current image, using predifined parameters
        /// </summary>
        /// <param name="haarObj">The object to be detected</param>
        /// <returns>The objects detected, one array per channel</returns>
        public Rectangle<double>[][] DetectHaarCascade(HaarCascade haarObj)
        {
            return DetectHaarCascade(haarObj, 1.1, 3, 1, new MCvSize(0, 0));
        }

        /// <summary>
        /// The function cvHaarDetectObjects finds rectangular regions in the given image that are likely to contain objects the cascade has been trained for and returns those regions as a sequence of rectangles. The function scans the image several times at different scales (see cvSetImagesForHaarClassifierCascade). Each time it considers overlapping regions in the image and applies the classifiers to the regions using cvRunHaarClassifierCascade. It may also apply some heuristics to reduce number of analyzed regions, such as Canny prunning. After it has proceeded and collected the candidate rectangles (regions that passed the classifier cascade), it groups them and returns a sequence of average rectangles for each large enough group. The default parameters (scale_factor=1.1, min_neighbors=3, flags=0) are tuned for accurate yet slow object detection. For a faster operation on real video images the settings are: scale_factor=1.2, min_neighbors=2, flags=CV_HAAR_DO_CANNY_PRUNING, min_size=&lt;minimum possible face size&gt; (for example, ~1/4 to 1/16 of the image area in case of video conferencing). 
        /// </summary>
        /// <param name="haarObj">Haar classifier cascade in internal representation</param>
        /// <param name="scaleFactor">The factor by which the search window is scaled between the subsequent scans, for example, 1.1 means increasing window by 10%</param>
        /// <param name="minNeighbors">Minimum number (minus 1) of neighbor rectangles that makes up an object. All the groups of a smaller number of rectangles than min_neighbors-1 are rejected. If min_neighbors is 0, the function does not any grouping at all and returns all the detected candidate rectangles, which may be useful if the user wants to apply a customized grouping procedure</param>
        /// <param name="flag">Mode of operation. Currently the only flag that may be specified is CV_HAAR_DO_CANNY_PRUNING. If it is set, the function uses Canny edge detector to reject some image regions that contain too few or too much edges and thus can not contain the searched object. The particular threshold values are tuned for face detection and in this case the pruning speeds up the processing.</param>
        /// <param name="minSize">Minimum window size. By default, it is set to the size of samples the classifier has been trained on (~20×20 for face detection)</param>
        /// <returns>The objects detected, one array per channel</returns>
        public Rectangle<double>[][] DetectHaarCascade(HaarCascade haarObj, double scaleFactor, int minNeighbors, int flag, MCvSize minSize)
        {
            using (MemStorage stor = new MemStorage())
            {
                Emgu.Utils.Converter<IntPtr, int, Rectangle<double>[]> detector =
                    delegate(IntPtr img, int channel)
                    {
                        IntPtr objects = CvInvoke.cvHaarDetectObjects(
                        img,
                        haarObj.Ptr,
                        stor.Ptr,
                        scaleFactor,
                        minNeighbors,
                        flag,
                        minSize);

                        int count = 0;
                        if (objects != IntPtr.Zero)
                        {
                            MCvSeq seq = (MCvSeq)Marshal.PtrToStructure(objects, typeof(MCvSeq));
                            count = seq.total;
                        }

                        Rectangle<double>[] recs = new Rectangle<double>[count];

                        if (count != 0)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                recs[i] = new Rectangle<double>(
                                    (MCvRect)Marshal.PtrToStructure(
                                        CvInvoke.cvGetSeqElem(objects, i),
                                        typeof(MCvRect)));
                            }
                            CvInvoke.cvClearSeq((IntPtr)objects);
                        }
                        return recs;
                    };

                Rectangle<double>[][] res = ForEachChannel(detector);
                return res;
            }
        }

        ///<summary> 
        ///Apply Hugh transform to find line segments. 
        ///The current image must be a binary image (eg. the edges as a result of the Canny edge detector) 
        ///</summary> 
        public LineSegment2D<int>[][] HughLinesBinary(double rhoResolution, double thetaResolution, int threshold, double minLineWidth, double gapBetweenLines)
        {
            using (MemStorage stor = new MemStorage())
            {
                Emgu.Utils.Converter<IntPtr, int, LineSegment2D<int>[]> detector =
                    delegate(IntPtr img, int channel)
                    {
                        IntPtr lines = CvInvoke.cvHoughLines2(img, stor.Ptr, CvEnum.HOUGH_TYPE.CV_HOUGH_PROBABILISTIC, rhoResolution, thetaResolution, threshold, minLineWidth, gapBetweenLines);
                        MCvSeq lineSeq = (MCvSeq)Marshal.PtrToStructure(lines, typeof(MCvSeq));
                        LineSegment2D<int>[] linesegs = new LineSegment2D<int>[lineSeq.total];
                        for (int i = 0; i < lineSeq.total; i++)
                        {
                            int[] val = new int[4];
                            Marshal.Copy(CvInvoke.cvGetSeqElem(lines, i), val, 0, 4);
                            linesegs[i] = new LineSegment2D<int>(
                                new Point2D<int>(val[0], val[1]),
                                new Point2D<int>(val[2], val[3]));
                        }
                        CvInvoke.cvClearSeq(lines);
                        return linesegs;
                    };
                LineSegment2D<int>[][] res = ForEachChannel(detector);
                return res;
            }
        }

        ///<summary> 
        ///First apply Canny Edge Detector on the current image, 
        ///then apply Hugh transform to find line segments 
        ///</summary>
        public LineSegment2D<int>[][] HughLines(C cannyThreshold, C cannyThresholdLinking, double rhoResolution, double thetaResolution, int threshold, double minLineWidth, double gapBetweenLines)
        {
            using (Image<C, D> canny = Canny(cannyThreshold, cannyThresholdLinking))
            {
                return canny.HughLinesBinary(rhoResolution, thetaResolution, threshold, minLineWidth, gapBetweenLines);
            }
        }

        ///<summary> 
        ///First apply Canny Edge Detector on the current image, 
        ///then apply Hugh transform to find circles 
        ///</summary>
        public Circle<float>[][] HughCircles(C cannyThreshold, C cannyThresholdLinking, double dp, double minDist, int minRadius, int maxRadius)
        {
            using (MemStorage stor = new MemStorage())
            {
                double[] c1 = cannyThreshold.Resize(4).Coordinate;
                double[] c2 = cannyThresholdLinking.Resize(4).Coordinate;
                Emgu.Utils.Converter<IntPtr, int, Circle<float>[]> detector =
                    delegate(IntPtr img, int channel)
                    {
                        IntPtr circlesSeqPtr = CvInvoke.cvHoughCircles(
                            img,
                            stor.Ptr,
                            CvEnum.HOUGH_TYPE.CV_HOUGH_GRADIENT,
                            dp,
                            minDist,
                            c1[channel],
                            c2[channel],
                            minRadius,
                            maxRadius);

                        Seq<MCvPoint3D32f> cirSeq = new Seq<MCvPoint3D32f>(circlesSeqPtr, stor);

                        return System.Array.ConvertAll<MCvPoint3D32f, Circle<float>>(cirSeq.ToArray(),
                            delegate(MCvPoint3D32f p)
                            {
                                return new Circle<float>(new Point2D<float>(p.x, p.y), p.z);
                            });
                    };
                Circle<float>[][] res = ForEachChannel(detector);

                return res;
            }
        }
        #endregion

        /// <summary>
        /// Returns the min / max location and values for the image
        /// For Image&lt;Bgr, Byte&gt; img and minmax = img.MinMax();
        /// minmax[0] contains the minmax locations and values for B channel and minmax[1] contains the minmax location for G channel etc.
        /// minmax[0][0] contains the min locations and values for B channel and minmax[0][1] contains the max locations and values for B channel.
        /// minmax[0][0].X and minmax[0][0].Y contains the X and Y coordinates for the min location and minmix[0][0].Z contains the minimum value for B channel
        /// </summary>
        /// <returns>
        /// Returns the min / max location and values for the image
        /// For Image&lt;Bgr, Byte&gt; img and minmax = img.MinMax();
        /// minmax[0] contains the minmax locations and values for B channel and minmax[1] contains the minmax location for G channel etc.
        /// minmax[0][0] contains the min locations and values for B channel and minmax[0][1] contains the max locations and values for B channel.
        /// minmax[0][0].X and minmax[0][0].Y contains the X and Y coordinates for the min location and minmix[0][0].Z contains the minimum value for B channel
        /// </returns>
        public Point3D<double>[][] MinMax()
        {
            int channelCount = Color.Dimension;
            Point3D<double>[][] res = new Point3D<double>[channelCount][];

            double minVal = 0, maxVal = 0;
            MCvPoint minLoc = new MCvPoint(0, 0);
            MCvPoint maxLoc = new MCvPoint(0, 0);

            if (channelCount == 1)
            {
                CvInvoke.cvMinMaxLoc(Ptr, ref minVal, ref maxVal, ref minLoc, ref maxLoc, IntPtr.Zero);
                res[0] = new Point3D<double>[2]
                {   new Point3D<double>(minLoc.x, minLoc.y, minVal),
                    new Point3D<double>(maxLoc.x, maxLoc.y, maxVal)
                };
            }
            else
            {
                for (int i = 0; i < channelCount; i++)
                {
                    CvInvoke.cvSetImageCOI(Ptr, i + 1);
                    CvInvoke.cvMinMaxLoc(Ptr, ref minVal, ref maxVal, ref minLoc, ref maxLoc, IntPtr.Zero);
                    res[i] = new Point3D<double>[2]
                    {   new Point3D<double>(minLoc.x, minLoc.y, minVal),
                        new Point3D<double>(maxLoc.x, maxLoc.y, maxVal)
                    };
                }
                CvInvoke.cvSetImageCOI(Ptr, 0);
            }

            return res;
        }

        /// <summary>
        /// Get or Set the color in the <paramref name="row"/>th row (y direction) and <paramref name="column"/>th column (x direction)
        /// </summary>
        /// <param name="row">the row (y direction) of the pixel </param>
        /// <param name="column">the column (x direction) of the pixel</param>
        /// <returns>the color in the <paramref name="row"/>th row and <paramref name="column"/>th column</returns>
        public C this[int row, int column]
        {
            get
            {
                C res = new C();
                res.CvScalar = CvInvoke.cvGet2D(Ptr, row, column);
                return res;
            }
            set
            {
                CvInvoke.cvSet2D(Ptr, row, column, value.CvScalar);
            }
        }

        /// <summary>
        /// Apply convertor and compute result for each channel of the image, for single channel image, apply converter directly, for multiple channel image, make a copy of each channel to a temperary image and apply the convertor
        /// </summary>
        /// <typeparam name="R">The return type</typeparam>
        /// <param name="conv">The converter such that accept the IntPtr of a single channel IplImage, and image channel index which returning result of type R</param>
        /// <returns>An array which contains result for each channel</returns>
        private R[] ForEachChannel<R>(Emgu.Utils.Converter<IntPtr, int, R> conv)
        {
            int channelCount = Color.Dimension;
            R[] res = new R[channelCount];
            if (channelCount == 1)
                res[0] = conv(Ptr, 0);
            else
            {
                IntPtr tmp = CvInvoke.cvCreateImage(new MCvSize(Width, Height), CvDepth, 1);
                for (int i = 0; i < channelCount; i++)
                {
                    CvInvoke.cvSetImageCOI(Ptr, i + 1);
                    CvInvoke.cvCopy(Ptr, tmp, IntPtr.Zero);
                    res[i] = conv(tmp, i);
                }
                CvInvoke.cvReleaseImage(ref tmp);
                CvInvoke.cvSetImageCOI(Ptr, 0);
            }
            return res;
        }

        /// <summary>
        /// If the image has only one channel, apply the action directly on the IntPtr of this image and <paramref name="image2"/>,
        /// otherwise, make copy each channel of this image to a temperary one, apply action on it and another temperory image and copy the resulting image back to image2
        /// </summary>
        /// <typeparam name="D2">The type of the depth of the <paramref name="dest"/> image</typeparam>
        /// <param name="act">The function which acepts the src IntPtr, dest IntPtr and index of the channel as input</param>
        /// <param name="dest">The destination image</param>
        private void ForEachChannel<D2>(Emgu.Utils.Action<IntPtr, IntPtr, int> act, Image<C, D2> dest)
        {
            int channelCount = Color.Dimension;
            if (channelCount == 1)
                act(Ptr, dest.Ptr, 0);
            else
            {
                using (Image<Gray, D> tmp1 = new Image<Gray, D>(Width, Height))
                using (Image<Gray, D2> tmp2 = new Image<Gray, D2>(dest.Width, dest.Height))
                {
                    for (int i = 0; i < channelCount; i++)
                    {
                        CvInvoke.cvSetImageCOI(Ptr, i + 1);
                        CvInvoke.cvSetImageCOI(dest.Ptr, i + 1);
                        CvInvoke.cvCopy(Ptr, tmp1.Ptr, IntPtr.Zero);
                        act(tmp1.Ptr, tmp2.Ptr, i);
                        CvInvoke.cvCopy(tmp2.Ptr, dest.Ptr, IntPtr.Zero);
                    }
                }
                CvInvoke.cvSetImageCOI(Ptr, 0);
                CvInvoke.cvSetImageCOI(dest.Ptr, 0);
            }
        }

        /// <summary>
        /// The function cvSobel calculates the image derivative by convolving the image with the appropriate kernel:
        /// dst(x,y) = dxorder+yodersrc/dxxorder•dyyorder |(x,y)
        /// The Sobel operators combine Gaussian smoothing and differentiation so the result is more or less robust to the noise. Most often, the function is called with (xorder=1, yorder=0, aperture_size=3) or (xorder=0, yorder=1, aperture_size=3) to calculate first x- or y- image derivative.
        /// </summary>
        /// <param name="xorder">Order of the derivative x</param>
        /// <param name="yorder">Order of the derivative y</param>
        /// <param name="aperture_size">Size of the extended Sobel kernel, must be 1, 3, 5 or 7. In all cases except 1, aperture_size ×aperture_size separable kernel will be used to calculate the derivative.</param>
        /// <returns>The result of the sobel edge detector</returns>
        public Image<C, D> Sobel(int xorder, int yorder, int aperture_size)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvSobel(Ptr, res.Ptr, xorder, yorder, aperture_size);
            return res;
        }

        /// <summary>
        /// The function cvLaplace calculates Laplacian of the source image by summing second x- and y- derivatives calculated using Sobel operator:
        /// dst(x,y) = d2src/dx2 + d2src/dy2
        /// Specifying aperture_size=1 gives the fastest variant that is equal to convolving the image with the following kernel:
        ///
        /// |0  1  0|
        /// |1 -4  1|
        /// |0  1  0|
        /// </summary>
        /// <param name="aperture_size">Aperture size </param>
        /// <returns>The Laplacian of the image</returns>
        public Image<C, D> Laplace(int aperture_size)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvLaplace(Ptr, res.Ptr, aperture_size);
            return res;
        }

        /// <summary>
        /// The function cvGoodFeaturesToTrack finds corners with big eigenvalues in the image. The function first calculates the minimal eigenvalue for every source image pixel using cvCornerMinEigenVal function and stores them in eig_image. Then it performs non-maxima suppression (only local maxima in 3x3 neighborhood remain). The next step is rejecting the corners with the minimal eigenvalue less than quality_level•max(eig_image(x,y)). Finally, the function ensures that all the corners found are distanced enough one from another by considering the corners (the most strongest corners are considered first) and checking that the distance between the newly considered feature and the features considered earlier is larger than min_distance. So, the function removes the features than are too close to the stronger features
        /// </summary>
        /// <param name="maxFeaturesPerChannel">The maximum features to be detected per channel</param>
        /// <param name="quality_level">Multiplier for the maxmin eigenvalue; specifies minimal accepted quality of image corners</param>
        /// <param name="min_distance">Limit, specifying minimum possible distance between returned corners; Euclidian distance is used. </param>
        /// <param name="block_size">Size of the averaging block, passed to underlying cvCornerMinEigenVal or cvCornerHarris used by the function</param>
        /// <param name="use_harris">If nonzero, Harris operator (cvCornerHarris) is used instead of default cvCornerMinEigenVal</param>
        /// <param name="k">Free parameter of Harris detector; used only if use_harris = true </param>
        /// <returns></returns>
        public Point2D<float>[][] GoodFeaturesToTrack(int maxFeaturesPerChannel, double quality_level, double min_distance, int block_size, bool use_harris, double k)
        {
            int channelCount = Color.Dimension;
            Point2D<float>[][] res = new Point2D<float>[channelCount][];

            float[,] coors = new float[maxFeaturesPerChannel, 2];

            using (Image<Gray, Single> eig_image = new Image<Gray, float>(Width, Height))
            using (Image<Gray, Single> tmp_image = new Image<Gray, float>(Width, Height))
            {
                Emgu.Utils.Converter<IntPtr, int, Point2D<float>[]> detector =
                    delegate(IntPtr img, int channel)
                    {
                        int corner_count = maxFeaturesPerChannel;
                        GCHandle handle = GCHandle.Alloc(coors, GCHandleType.Pinned);
                        CvInvoke.cvGoodFeaturesToTrack(
                            Ptr,
                            eig_image.Ptr,
                            tmp_image.Ptr,
                            handle.AddrOfPinnedObject(),
                            ref corner_count,
                            quality_level,
                            min_distance,
                            IntPtr.Zero,
                            block_size, 
                            use_harris ? 1 : 0, 
                            k);
                        handle.Free();

                        Point2D<float>[] pts = new Point2D<float>[corner_count];
                        for (int i = 0; i < corner_count; i++)
                            pts[i] = new Point2D<float>(coors[i,0], coors[i,1]);
                        return pts;
                    };

                res = ForEachChannel(detector);
            }

            return res;
        }

        /// <summary>
        /// The function cvMatchTemplate is similiar to cvCalcBackProjectPatch. It slids through image, compares overlapped patches of size w×h with templ using the specified method and stores the comparison results to result
        /// </summary>
        /// <param name="template">Searched template; must be not greater than the source image and the same data type as the image</param>
        /// <param name="method">Specifies the way the template must be compared with image regions </param>
        /// <returns>The comparison result</returns>
        public Image<C, D> MatchTemplate(Image<C, D> template, CvEnum.TM_TYPE method)
        {
            Image<C, D> res = new Image<C, D>(Width - template.Width + 1, Height - template.Height + 1);
            CvInvoke.cvMatchTemplate(Ptr, template.Ptr, res.Ptr, method);
            return res;
        }

        /// <summary>
        /// The function cvSnakeImage updates snake in order to minimize its total energy that is a sum of internal energy that depends on contour shape (the smoother contour is, the smaller internal energy is) and external energy that depends on the energy field and reaches minimum at the local energy extremums that correspond to the image edges in case of image gradient.
        ///The parameter criteria.epsilon is used to define the minimal number of points that must be moved during any iteration to keep the iteration process running. 
        ///If at some iteration the number of moved points is less than criteria.epsilon or the function performed criteria.max_iter iterations, the function terminates. 
        /// </summary>
        /// <param name="c">Some existing contour</param>
        /// <param name="alpha">Weight[s] of continuity energy, single float or array of length floats, one per each contour point</param>
        /// <param name="beta">Weight[s] of curvature energy, similar to alpha.</param>
        /// <param name="gamma">Weight[s] of image energy, similar to alpha.</param>
        /// <param name="windowSize">Size of neighborhood of every point used to search the minimum, both win.width and win.height must be odd</param>
        /// <param name="tc">Termination criteria</param>
        /// <param name="storage"> the memory storage used by the resulting sequence</param>
        /// <returns>The snake[d] contour</returns>
        public Seq<MCvPoint> Snake(Seq<MCvPoint> c, float alpha, float beta, float gamma, Point2D<int> windowSize, MCvTermCriteria tc, MemStorage storage)
        {
            int count = c.Total;

            IntPtr points = Marshal.AllocHGlobal(count * 2 * sizeof(int));

            CvInvoke.cvCvtSeqToArray(c.Ptr, points, new MCvSlice(0, 0x3fffffff));
            CvInvoke.cvSnakeImage(
                Ptr,
                points,
                count,
                new float[1] { alpha },
                new float[1] { beta },
                new float[1] { gamma },
                1,
                new MCvSize(windowSize.X, windowSize.Y),
                tc,
                1);
            IntPtr rSeq = CvInvoke.cvCreateSeq(
                (int)CvEnum.SEQ_TYPE.CV_SEQ_POLYGON,
                Marshal.SizeOf(typeof(MCvContour)),
                Marshal.SizeOf(typeof(MCvPoint)),
                storage.Ptr);

            CvInvoke.cvSeqPushMulti(rSeq, points, count, false);
            Marshal.FreeHGlobal(points);

            return new Seq<MCvPoint>(rSeq, storage);

        }

        ///<summary> 
        /// Make a clone of the image using a mask, if ROI is set, only copy the ROI 
        /// </summary> 
        ///<returns> A clone of the image</returns>
        public Image<C, D> Clone(Image<Gray, Byte> mask)
        {
            //it is necessary to clear the color such that the non-masked area are set to 0
            Image<C, D> res = BlankClone(new C());
            CvInvoke.cvCopy(Ptr, res.Ptr, mask.Ptr);
            return res;
        }

        ///<summary> Make a clone of the image, if ROI is set, only copy the ROI</summary>
        ///<returns> A clone of the image</returns>
        public Image<C, D> Clone()
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvCopy(Ptr, res.Ptr, IntPtr.Zero);
            return res;
        }

        ///<summary> Copy the current image to another one </summary>
        public void Copy(Image<C, D> dest)
        {
            CvInvoke.cvCopy(Ptr, dest.Ptr, IntPtr.Zero);
        }

        /// <summary>
        /// Copy the masked area of this image to destination
        /// </summary>
        /// <param name="dest">the destination to copy to</param>
        /// <param name="mask">the mask for copy</param>
        public void Copy(Image<C, D> dest, Image<Gray, Byte> mask)
        {
            CvInvoke.cvCopy(Ptr, dest.Ptr, mask.Ptr);
        }

        #region And Methods
        ///<summary> Perform an elementwise AND operation with another image and return the result</summary>
        ///<param name="img2">The second image for the AND operation</param>
        ///<returns> The result of the AND operation</returns>
        public Image<C, D> And(Image<C, D> img2)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAnd(Ptr, img2.Ptr, res.Ptr, IntPtr.Zero);
            return res;
        }

        ///<summary> 
        ///Perform an elementwise AND operation with another image, using a mask, and return the result
        ///</summary>
        ///<param name="img2">The second image for the AND operation</param>
        ///<param name="mask">The mask for the AND operation</param>
        ///<returns> The result of the AND operation</returns>
        public Image<C, D> And(Image<C, D> img2, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAnd(Ptr, img2.Ptr, res.Ptr, mask.Ptr);
            return res;
        }

        ///<summary> Perform an binary AND operation with some color</summary>
        ///<param name="val">The color for the AND operation</param>
        ///<returns> The result of the AND operation</returns>
        public Image<C, D> And(C val)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAndS(Ptr, val.CvScalar, res.Ptr, IntPtr.Zero);
            return res;
        }

        ///<summary> Perform an binary AND operation with some color using a mask</summary>
        ///<param name="val">The color for the AND operation</param>
        ///<param name="mask">The mask for the AND operation</param>
        ///<returns> The result of the AND operation</returns>
        public Image<C, D> And(C val, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAndS(Ptr, val.CvScalar, res.Ptr, mask.Ptr);
            return res;
        }
        #endregion

        #region Or Methods
        ///<summary> Perform an elementwise OR operation with another image and return the result</summary>
        ///<param name="img2">The second image for the OR operation</param>
        ///<returns> The result of the OR operation</returns>
        public Image<C, D> Or(Image<C, D> img2)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvOr(Ptr, img2.Ptr, res.Ptr, IntPtr.Zero);
            return res;
        }
        ///<summary> Perform an elementwise OR operation with another image, using a mask, and return the result</summary>
        ///<param name="img2">The second image for the OR operation</param>
        ///<param name="mask">The mask for the OR operation</param>
        ///<returns> The result of the OR operation</returns>
        public Image<C, D> Or(Image<C, D> img2, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvOr(Ptr, img2.Ptr, res.Ptr, mask.Ptr);
            return res;
        }

        ///<summary> Perform an elementwise OR operation with some color</summary>
        ///<param name="val">The value for the OR operation</param>
        ///<returns> The result of the OR operation</returns>
        public Image<C, D> Or(C val)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvOrS(Ptr, val.CvScalar, res.Ptr, IntPtr.Zero);
            return res;
        }
        ///<summary> Perform an elementwise OR operation with some color using a mask</summary>
        ///<param name="val">The color for the OR operation</param>
        ///<param name="mask">The mask for the OR operation</param>
        ///<returns> The result of the OR operation</returns>
        public Image<C, D> Or(C val, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvOrS(Ptr, val.CvScalar, res.Ptr, mask.Ptr);
            return res;
        }
        #endregion 

        #region Xor Methods
        ///<summary> Perform an elementwise XOR operation with another image and return the result</summary>
        ///<param name="img2">The second image for the XOR operation</param>
        ///<returns> The result of the XOR operation</returns>
        public Image<C, D> Xor(Image<C, D> img2)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvXor(Ptr, img2.Ptr, res.Ptr, IntPtr.Zero);
            return res;
        }

        /// <summary>
        /// Perform an elementwise XOR operation with another image, using a mask, and return the result
        /// </summary>
        /// <param name="img2">The second image for the XOR operation</param>
        /// <param name="mask">The mask for the XOR operation</param>
        /// <returns>The result of the XOR operation</returns>
        public Image<C, D> Xor(Image<C, D> img2, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvXor(Ptr, img2.Ptr, res.Ptr, mask.Ptr);
            return res;
        }

        /// <summary> 
        /// Perform an binary XOR operation with some color
        /// </summary>
        /// <param name="val">The value for the XOR operation</param>
        /// <returns> The result of the XOR operation</returns>
        public Image<C, D> Xor(C val)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvXorS(Ptr, val.CvScalar, res.Ptr, IntPtr.Zero);
            return res;
        }

        /// <summary>
        /// Perform an binary XOR operation with some color using a mask
        /// </summary>
        /// <param name="val">The color for the XOR operation</param>
        /// <param name="mask">The mask for the XOR operation</param>
        /// <returns> The result of the XOR operation</returns>
        public Image<C, D> Xor(C val, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvXorS(Ptr, val.CvScalar, res.Ptr, mask.Ptr);
            return res;
        }
        #endregion 

        ///<summary> 
        ///Compute the complement image
        ///</summary>
        ///<returns> The complement image</returns>
        public Image<C, D> Not()
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvNot(Ptr, res.Ptr);
            return res;
        }

        ///<summary> Find the elementwise maximum value </summary>
        ///<param name="img2">The second image for the Max operation</param>
        ///<returns> An image where each pixel is the maximum of <i>this</i> image and the parameter image</returns>
        public Image<C, D> Max(Image<C, D> img2)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvMax(Ptr, img2.Ptr, res.Ptr);
            return res;
        }

        #region Substraction methods
        ///<summary> Elementwise subtract another image from the current image </summary>
        ///<param name="img2">The second image to be subtraced from the current image</param>
        ///<returns> The result of elementwise subtracting img2 from the current image</returns>
        public Image<C, D> Sub(Image<C, D> img2)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvSub(Ptr, img2.Ptr, res.Ptr, IntPtr.Zero);
            return res;
        }

        ///<summary> Elementwise subtrace another image from the current image, using a mask</summary>
        ///<param name="img2">The image to be subtraced from the current image</param>
        ///<param name="mask">The mask for the subtract operation</param>
        ///<returns> The result of elementwise subtrating img2 from the current image, using the specific mask</returns>
        public Image<C, D> Sub(Image<C, D> img2, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvSub(Ptr, img2.Ptr, res.Ptr, mask.Ptr);
            return res;
        }

        ///<summary> Elementwise subtrace a color from the current image</summary>
        ///<param name="val">The color value to be subtraced from the current image</param>
        ///<returns> The result of elementwise subtracting color 'val' from the current image</returns>
        public Image<C, D> Sub(C val)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvSubS(Ptr, val.CvScalar, res.Ptr, IntPtr.Zero);
            return res;
        }

        /// <summary>
        /// result = val - this
        /// </summary>
        /// <param name="val">the value which subtract this image</param>
        /// <returns>val - this</returns>
        public Image<C, D> SubR(C val)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvSubRS(Ptr, val.CvScalar, res.Ptr, IntPtr.Zero);
            return res;
        }

        /// <summary>
        /// result = val - this, using a mask
        /// </summary>
        /// <param name="val">the value which subtract this image</param>
        /// <param name="mask"> The mask for substraction</param>
        /// <returns>val - this, with mask</returns>
        public Image<C, D> SubR(C val, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvSubRS(Ptr, val.CvScalar, res.Ptr, mask.Ptr);
            return res;
        }
        #endregion 

        #region Addition methods
        ///<summary> Elementwise add another image with the current image </summary>
        ///<param name="img2">The image to be added to the current image</param>
        ///<returns> The result of elementwise adding img2 to the current image</returns>
        public Image<C, D> Add(Image<C, D> img2)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAdd(Ptr, img2.Ptr, res.Ptr, IntPtr.Zero);
            return res;
        }
        ///<summary> Elementwise add <paramref name="img2"/> with the current image, using a mask</summary>
        ///<param name="img2">The image to be added to the current image</param>
        ///<param name="mask">The mask for the add operation</param>
        ///<returns> The result of elementwise adding img2 to the current image, using the specific mask</returns>
        public Image<C, D> Add(Image<C, D> img2, Image<Gray, Byte> mask)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAdd(Ptr, img2.Ptr, res.Ptr, mask.Ptr);
            return res;
        }
        ///<summary> Elementwise add a color <paramref name="val"/> to the current image</summary>
        ///<param name="val">The color value to be added to the current image</param>
        ///<returns> The result of elementwise adding color <paramref name="val"/> from the current image</returns>
        public Image<C, D> Add(C val)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAddS(Ptr, val.CvScalar, res.Ptr, IntPtr.Zero);
            return res;
        }
        #endregion

        #region Multiplication methods
        ///<summary> Elementwise multiply another image with the current image and the <paramref name="scale"/></summary>
        ///<param name="img2">The image to be elementwise multiplied to the current image</param>
        ///<param name="scale">The scale to be multiplied</param>
        ///<returns> this .* img2 * scale </returns>
        public Image<C, D> Mul(Image<C, D> img2, double scale)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvMul(Ptr, img2.Ptr, res.Ptr, scale);
            return res;
        }
        ///<summary> Elementwise multiply <paramref name="img2"/> with the current image</summary>
        ///<param name="img2">The image to be elementwise multiplied to the current image</param>
        ///<returns> this .* img2 </returns>
        public Image<C, D> Mul(Image<C, D> img2)
        {
            return Mul(img2, 1.0);
        }

        ///<summary> Elementwise multiply the current image with <paramref name="scale"/></summary>
        ///<param name="scale">The scale to be multiplied</param>
        ///<returns> The scaled image </returns>
        public Image<C, D> Mul(double scale)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvConvertScale(Ptr, res.Ptr, scale, 0.0);
            return res;
        }
        #endregion

        ///<summary> Return the weighted sum such that: res = this * alpha + img2 * beta + gamma</summary>
        public Image<C, D> AddWeighted(Image<C, D> img2, double alpha, double beta, double gamma)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAddWeighted(Ptr, alpha, img2.Ptr, beta, gamma, res.Ptr);
            return res;
        }

        ///<summary>Scale the image to the specific size </summary>
        ///<param name="width">The width of the returned image.</param>
        ///<param name="height">The height of the returned image.</param>
        public Image<C, D> Resize(int width, int height)
        {
            Image<C, D> imgScale = new Image<C, D>(width, height);
            CvInvoke.cvResize(Ptr, imgScale.Ptr, CvEnum.INTER.CV_INTER_LINEAR);
            return imgScale;
        }

        /// <summary>
        /// Scale the image to the specific size
        /// </summary>
        /// <param name="width">The width of the returned image.</param>
        /// <param name="height">The height of the returned image.</param>
        /// <param name="preserverScale">if true, the scale is preservered and the resulting image has maximum width(height) possible that is less than <paramref name="width"/> (<paramref name="height"/>), if false, this function is equaivalent to Resize(int width, int height)</param>
        /// <returns></returns>
        public Image<C, D> Resize(int width, int height, bool preserverScale)
        {
            if (preserverScale)
            {
                return Resize(Math.Min((double)width / Width, (double)height / Height));
            }
            else
            {
                return Resize(width, height);
            }
        }

        ///<summary>Scale the image to the specific size: width *= scale; height *= scale  </summary>
        public Image<C, D> Resize(double scale)
        {
            return Resize(
                (int)(Width * scale),
                (int)(Height * scale));
        }

        #region Image color and depth conversion
        private static CvEnum.COLOR_CONVERSION GetColorCvtCode(ColorType srcType, ColorType destType)
        {
            ColorInfo srcInfo = (ColorInfo)srcType.GetType().GetCustomAttributes(typeof(ColorInfo), true)[0];
            ColorInfo destInfo = (ColorInfo)destType.GetType().GetCustomAttributes(typeof(ColorInfo), true)[0];

            String key = String.Format("CV_{0}2{1}", srcInfo.ConversionCodeName, destInfo.ConversionCodeName);
            return (CvEnum.COLOR_CONVERSION)Enum.Parse(typeof(CvEnum.COLOR_CONVERSION), key, true);
        }

        ///<summary> Convert the current image to the specific color and depth </summary>
        ///<typeparam name="C2"> The type of color to be converted to </typeparam>
        ///<typeparam name="D2"> The type of pixel depth to be converted to </typeparam>
        ///<returns> Image of the specific color and depth </returns>
        public Image<C2, D2> Convert<C2, D2>() where C2 : Emgu.CV.ColorType, new()
        {
            Image<C2, D2> res = new Image<C2, D2>(Width, Height);

            if (typeof(C) == typeof(C2))
            {   //same color
                if (typeof(D) == typeof(D2))
                {   //same depth
                    CvInvoke.cvCopy(Ptr, res.Ptr, IntPtr.Zero);
                }
                else
                {   //different depth
                    Emgu.Utils.Action<IntPtr, IntPtr, Type, Type> convertDepth =
                        delegate(IntPtr src, IntPtr dest, Type t1, Type t2)
                        {
                            if (t1 == typeof(Single) && t2 == typeof(Byte))
                            {
                                double min = 0.0, max = 0.0, scale, shift;
                                MCvPoint p1 = new MCvPoint();
                                MCvPoint p2 = new MCvPoint();
                                CvInvoke.cvMinMaxLoc(src, ref min, ref max, ref p1, ref p2, IntPtr.Zero);
                                scale = (max == min) ? 0.0 : 256.0 / (max - min);
                                shift = (scale == 0) ? min : -min * scale;
                                CvInvoke.cvConvertScaleAbs(src, dest, scale, shift);
                            }
                            else
                            {
                                CvInvoke.cvConvertScale(src, dest, 1.0, 0.0);
                            }
                        };

                    convertDepth(Ptr, res.Ptr, typeof(D), typeof(D2));
                }
            }
            else
            {   //different color
                Emgu.Utils.Action<IntPtr, IntPtr, C, C2> convertColor =
                    delegate(IntPtr src, IntPtr dest, C c1, C2 c2)
                    {
                        try
                        {
                            // if the direct conversion exist, apply the conversion
                            CvInvoke.cvCvtColor(src, dest, GetColorCvtCode(c1, c2));
                        }
                        catch (Exception)
                        {
                            //if a direct conversion doesn't exist, apply a two step conversion
                            using (Image<Bgr, D> tmp = new Image<Bgr, D>(Width, Height))
                            {
                                CvInvoke.cvCvtColor(src, tmp.Ptr, GetColorCvtCode(c1, tmp.Color));
                                CvInvoke.cvCvtColor(tmp.Ptr, dest, GetColorCvtCode(tmp.Color, c2));
                            }
                        }
                    };

                if (typeof(D) == typeof(D2))
                {   //same depth
                    convertColor(Ptr, res.Ptr, new C(), new C2());
                }
                else
                {   //different depth
                    using (Image<C, D2> tmp = Convert<C, D2>())
                        convertColor(tmp.Ptr, res.Ptr, new C(), new C2());
                }
            }

            return res;
        }

        ///<summary> Convert the current image to the specific depth, at the same time scale and shift the values of the pixel</summary>
        ///<param name="scale"> The value to be multipled with the pixel </param>
        ///<param name="shift"> The value to be added to the pixel</param>
        ///<returns> Image of the specific depth, val = val * scale + shift </returns>
        public Image<C, D2> ConvertScale<D2>(double scale, double shift)
        {
            Image<C, D2> res = new Image<C, D2>(Width, Height);

            if (typeof(D2) == typeof(System.Byte))
                CvInvoke.cvConvertScaleAbs(Ptr, res.Ptr, scale, shift);
            else
                CvInvoke.cvConvertScale(Ptr, res.Ptr, scale, shift);
            return res;
        }
        #endregion

        ///<summary> Create an image of the same size with the specific color</summary>
        ///<param name="color"> The color of the new image</param>
        ///<returns> The image of the same size as <I>this</I> with the specific color</returns>
        public Image<C, D> BlankClone(C color)
        {
            return new Image<C, D>(Width, Height, color);
        }

        ///<summary> Create an image of the same size <b> warning: the initial pixel in the image contains random value</b></summary>
        ///<returns> The image of the same size as <I>this</I> with random color</returns>
        public Image<C, D> BlankClone()
        {
            return new Image<C, D>(Width, Height);
        }

        /// <summary>
        /// Return parameters based on ROI
        /// </summary>
        /// <param name="ptr">The Pointer to the IplImage</param>
        /// <param name="start">The address of thepointer that point to the start of the Bytes taken into consideration ROI</param>
        /// <param name="elementCount">ROI.Width * ColorType.Dimension</param>
        /// <param name="byteWidth">The number of bytes in a row taken into consideration ROI</param>
        /// <param name="rows">The number of rows taken into consideration ROI</param>
        /// <param name="widthStep">The width step required to jump to the next row</param>
        protected static void RoiParam(IntPtr ptr, out int start, out int rows, out int elementCount, out int byteWidth, out int widthStep)
        {
            MIplImage ipl = (MIplImage)Marshal.PtrToStructure(ptr, typeof(MIplImage));
            start = ipl.imageData.ToInt32();
            widthStep = ipl.widthStep;

            if (ipl.roi != IntPtr.Zero)
            {
                MCvRect rec = CvInvoke.cvGetImageROI(ptr);
                elementCount = (int)rec.width * ipl.nChannels;
                byteWidth = (ipl.depth >> 3) * elementCount;

                start += (int)rec.y * widthStep
                        + (ipl.depth >> 3) * (int)rec.x;
                rows = (int)rec.height;
            }
            else
            {
                byteWidth = widthStep;
                elementCount = ipl.width * ipl.nChannels;
                rows = ipl.height;
            }
        }

        #region Conversion with Bitmap
        /// <summary>
        /// The AsBitmap function provide a more efficient way to convert Image&lt;Gray, Byte&gt; and Image&lt;Bgr, Byte&gt; into Bitmap
        /// The image data is <b>shared</b> by the Image object and the Bitmap, if you change the pixel value on the Bitmap, the pixel values 
        /// on the Image object is changed as well!
        /// On other image types this function is the same as ToBitmap()
        /// <b>Take extra caution not to use the Bitmap after the Image object is disposed</b>
        /// </summary>
        /// <returns>A bitmap which shares image data with IplImage</returns>
        public Bitmap AsBitmap()
        {
            if (typeof(C) == typeof(Gray) && typeof(D) == typeof(Byte))
            {
                IntPtr scan0 = IntPtr.Zero;
                int step = 0;
                MCvSize size = new MCvSize();
                CvInvoke.cvGetRawData(Ptr, ref scan0, ref step, ref size);

                Bitmap bmp = new Bitmap(
                    size.width,
                    size.height,
                    step,
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed,
                    scan0
                    );
                bmp.Palette = Utils.GrayscalePalette;
                return bmp;
            }
            else if (typeof(C) == typeof(Bgr) && typeof(D) == typeof(Byte))
            {
                IntPtr scan0 = IntPtr.Zero;
                int step = 0;
                MCvSize size = new MCvSize();
                CvInvoke.cvGetRawData(Ptr, ref scan0, ref step, ref size);

                return new Bitmap(
                    size.width,
                    size.height,
                    step,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                    scan0);
            }
            else
            {
                return ToBitmap();
            }
        }

        ///<summary> Convert this image into Bitmap (for better performance on Image&lt;Gray, Byte&gt; and Image&lt;Bgr, Byte&gt;, consider AsBitmap() </summary>
        ///<returns> The same image in Bitmap format </returns>
        public Bitmap ToBitmap()
        {
            IntPtr ptr = Ptr;
            if (typeof(C) == typeof(Gray)) // if this is a gray scale image
            {
                #region convert it to depth of Byte if it is not
                Image<Gray, Byte> img8UGray = null;
                if (typeof(D) != typeof(Byte))
                {
                    img8UGray = Convert<Gray, Byte>();
                    ptr = img8UGray.Ptr;
                }
                #endregion

                Bitmap image = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
                System.Drawing.Imaging.BitmapData data = image.LockBits(
                    new System.Drawing.Rectangle(0, 0, Width, Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
                int dataPtr = data.Scan0.ToInt32();

                int start, elementCount, byteWidth, rows, widthStep;
                RoiParam(ptr, out start, out rows, out elementCount, out byteWidth, out widthStep);
                for (int row = 0; row < data.Height; row++, start += widthStep, dataPtr += data.Stride)
                    Emgu.Utils.memcpy((IntPtr)dataPtr, (IntPtr)start, data.Stride);

                image.UnlockBits(data);

                if (img8UGray != null)
                    img8UGray.Dispose();

                image.Palette = Utils.GrayscalePalette;

                return image;
            }
            else //if this is a multiple channel image
            {
                #region convert it to Bgr Byte image if it is not
                Image<Bgr, Byte> img8UBgr = null;
                if (!(typeof(C) == typeof(Bgr) && typeof(D) == typeof(Byte)))
                {
                    img8UBgr = Convert<Bgr, Byte>();
                    ptr = img8UBgr.Ptr;
                }
                #endregion

                //create the bitmap and get the pointer to the data
                Bitmap image = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                System.Drawing.Imaging.BitmapData data = image.LockBits(
                    new System.Drawing.Rectangle(0, 0, Width, Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                int dataPtr = data.Scan0.ToInt32();

                int start, elementCount, byteWidth, rows, widthStep;
                RoiParam(ptr, out start, out rows, out elementCount, out byteWidth, out widthStep);
                for (int row = 0; row < data.Height; row++, start += widthStep, dataPtr += data.Stride)
                    Emgu.Utils.memcpy((IntPtr)dataPtr, (IntPtr)start, data.Stride);

                image.UnlockBits(data);

                if (img8UBgr != null)
                    img8UBgr.Dispose();

                return image;
            }
        }

        ///<summary> Create a Bitmap image of certain size</summary>
        ///<param name="width">The width of the bitmap</param>
        ///<param name="height"> The height of the bitmap</param>
        ///<returns> This image in Bitmap format of the specific size</returns>
        public Bitmap ToBitmap(int width, int height)
        {
            using (Image<C, D> scaleImage = Resize(width, height))
                return scaleImage.ToBitmap();
        }
        #endregion

        ///<summary> performs a convolution using the provided kernel </summary>
        public Image<C, Single> Convolution(ConvolutionKernelF kernel)
        {
            bool isFloat = (typeof(D) == typeof(Single));

            Emgu.Utils.Action<IntPtr, IntPtr, int> act =
                delegate(IntPtr src, IntPtr dest, int channel)
                {
                    IntPtr srcFloat = src;
                    if (!isFloat)
                    {
                        srcFloat = CvInvoke.cvCreateImage(new MCvSize(Width, Height), CvEnum.IPL_DEPTH.IPL_DEPTH_32F, 1);
                        CvInvoke.cvConvertScale(src, srcFloat, 1.0, 0.0);
                    }

                    //perform the convolution operation
                    CvInvoke.cvFilter2D(
                        srcFloat,
                        dest,
                        kernel.Ptr,
                        kernel.Center.CvPoint);

                    if (!isFloat)
                    {
                        CvInvoke.cvReleaseImage(ref srcFloat);
                    }
                };

            Image<C, Single> res = new Image<C, Single>(Width, Height);
            ForEachChannel(act, res);

            return res;
        }

        ///<summary>
        ///The function PyrDown performs downsampling step of Gaussian pyramid decomposition. 
        ///First it convolves <i>this</i> image with the specified filter and then downsamples the image 
        ///by rejecting even rows and columns.
        ///</summary>
        ///<returns> The downsampled image</returns>
        public Image<C, D> PyrDown()
        {
            Image<C, D> res = new Image<C, D>(Width >> 1, Height >> 1);
            CvInvoke.cvPyrDown(Ptr, res.Ptr, CvEnum.FILTER_TYPE.CV_GAUSSIAN_5x5);
            return res;
        }

        ///<summary>
        ///The function cvPyrUp performs up-sampling step of Gaussian pyramid decomposition. 
        ///First it upsamples <i>this</i> image by injecting even zero rows and columns and then convolves 
        ///result with the specified filter multiplied by 4 for interpolation. 
        ///So the resulting image is four times larger than the source image.
        ///</summary>
        ///<returns> The upsampled image</returns>
        public Image<C, D> PyrUp()
        {
            Image<C, D> res = new Image<C, D>(Width << 1, Height << 1);
            CvInvoke.cvPyrUp(Ptr, res.Ptr, CvEnum.FILTER_TYPE.CV_GAUSSIAN_5x5);
            return res;
        }

        ///<summary> Perform Gaussian Smoothing in the current image and return the result </summary>
        ///<param name="kernelSize"> The size of the Gaussian kernel (<paramref>kernelSize</paramref> x <paramref>kernelSize</paramref>)</param>
        ///<returns> The smoothed image</returns>
        public Image<C, D> GaussianSmooth(int kernelSize) { return GaussianSmooth(kernelSize, 0, 0); }

        ///<summary> Perform Gaussian Smoothing in the current image and return the result </summary>
        ///<param name="kernelWidth"> The width of the Gaussian kernel</param>
        ///<param name="kernelHeight"> The height of the Gaussian kernel</param>
        ///<param name="sigma"> The standard deviation of the Gaussian kernel</param>
        ///<returns> The smoothed image</returns>
        public Image<C, D> GaussianSmooth(int kernelWidth, int kernelHeight, double sigma)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvSmooth(Ptr, res.Ptr, CvEnum.SMOOTH_TYPE.CV_GAUSSIAN, kernelWidth, kernelHeight, sigma, 0);
            return res;
        }

        ///<summary> Update Running Average. <i>this</i> = (1-alpha)*<i>this</i> + alpha*img) </summary>
        public void RunningAvg(Image<C, D> img, double alpha)
        {
            CvInvoke.cvRunningAvg(img.Ptr, Ptr, alpha, IntPtr.Zero);
        }

        /// <summary>
        /// Calculates spatial and central moments up to the third order and writes them to moments. The moments may be used then to calculate gravity center of the shape, its area, main axises and various shape characeteristics including 7 Hu invariants.
        /// </summary>
        /// <param name="binary">If the flag is true, all the zero pixel values are treated as zeroes, all the others are treated as 1’s</param>
        /// <returns>spatial and central moments up to the third order</returns>
        public Moment Moment(bool binary)
        {
            Moment m = new Moment();
            int flag = binary ? 1 : 0;
            CvInvoke.cvMoments(Ptr, m.Ptr, flag);
            return m;
        }

        #region Threshold methods
        ///<summary> 
        ///the base threshold method shared by public threshold functions 
        ///</summary>
        private void ThresholdBase(Image<C, D> dest, C threshold, C max_value, CvEnum.THRESH thresh_type)
        {
            double[] t = threshold.Resize(4).Coordinate;
            double[] m = max_value.Resize(4).Coordinate;
            Emgu.Utils.Action<IntPtr, IntPtr, int> act =
                delegate(IntPtr src, IntPtr dst, int channel)
                {
                    CvInvoke.cvThreshold(src, dst, t[channel], m[channel], thresh_type);
                };
            ForEachChannel<D>(act, dest);
        }

        ///<summary> Threshold the image such that: dst(x,y) = src(x,y), if src(x,y)>threshold;  0, otherwise </summary>
        ///<returns> dst(x,y) = src(x,y), if src(x,y)>threshold;  0, otherwise </returns>
        public Image<C, D> ThresholdToZero(C threshold)
        {
            Image<C, D> res = BlankClone();
            ThresholdBase(res, threshold, new C(), CvEnum.THRESH.CV_THRESH_TOZERO);
            return res;
        }
        ///<summary> Threshold the image such that: dst(x,y) = 0, if src(x,y)>threshold;  src(x,y), otherwise </summary>
        public Image<C, D> ThresholdToZeroInv(C threshold)
        {
            Image<C, D> res = BlankClone();
            ThresholdBase(res, threshold, new C(), CvEnum.THRESH.CV_THRESH_TOZERO_INV);
            return res;
        }
        ///<summary> Threshold the image such that: dst(x,y) = threshold, if src(x,y)>threshold; src(x,y), otherwise </summary>
        public Image<C, D> ThresholdTrunc(C threshold)
        {
            Image<C, D> res = BlankClone();
            ThresholdBase(res, threshold, new C(), CvEnum.THRESH.CV_THRESH_TRUNC);
            return res;
        }
        ///<summary> Threshold the image such that: dst(x,y) = max_value, if src(x,y)>threshold; 0, otherwise </summary>
        public Image<C, D> ThresholdBinary(C threshold, C maxValue)
        {
            Image<C, D> res = BlankClone();
            ThresholdBase(res, threshold, maxValue, CvEnum.THRESH.CV_THRESH_BINARY);
            return res;
        }
        ///<summary> Threshold the image such that: dst(x,y) = 0, if src(x,y)>threshold;  max_value, otherwise </summary>
        public Image<C, D> ThresholdBinaryInv(C threshold, C maxValue)
        {
            Image<C, D> res = BlankClone();
            ThresholdBase(res, threshold, maxValue, CvEnum.THRESH.CV_THRESH_BINARY_INV);
            return res;
        }
        ///<summary> Threshold the image inplace such that: dst(x,y) = src(x,y), if src(x,y)>threshold;  0, otherwise </summary>
        public void _ThresholdToZero(C threshold)
        {
            ThresholdBase(this, threshold, new C(), CvEnum.THRESH.CV_THRESH_TOZERO);
        }
        ///<summary> Threshold the image inplace such that: dst(x,y) = 0, if src(x,y)>threshold;  src(x,y), otherwise </summary>
        public void _ThresholdToZeroInv(C threshold)
        {
            ThresholdBase(this, threshold, new C(), CvEnum.THRESH.CV_THRESH_TOZERO_INV);
        }
        ///<summary> Threshold the image inplace such that: dst(x,y) = threshold, if src(x,y)>threshold; src(x,y), otherwise </summary>
        public void _ThresholdTrunc(C threshold)
        {
            ThresholdBase(this, threshold, new C(), CvEnum.THRESH.CV_THRESH_TRUNC);
        }
        ///<summary> Threshold the image inplace such that: dst(x,y) = max_value, if src(x,y)>threshold; 0, otherwise </summary>
        public void _ThresholdBinary(C threshold, C max_value)
        {
            ThresholdBase(this, threshold, max_value, CvEnum.THRESH.CV_THRESH_BINARY);
        }
        ///<summary> Threshold the image inplace such that: dst(x,y) = 0, if src(x,y)>threshold;  max_value, otherwise </summary>
        public void _ThresholdBinaryInv(C threshold, C max_value)
        {
            ThresholdBase(this, threshold, max_value, CvEnum.THRESH.CV_THRESH_BINARY_INV);
        }
        #endregion

        ///<summary> 
        ///Split current Image into an array of gray scale images where each element 
        ///in the array represent a single color channel of the original image
        ///</summary>
        ///<returns> 
        ///An array of gray scale images where each element 
        ///in the array represent a single color channel of the original image 
        ///</returns>
        public Image<Gray, D>[] Split()
        {
            int channelCount = Color.Dimension;
            Image<Gray, D>[] res = new Image<Gray, D>[channelCount];
            IntPtr[] a = new IntPtr[4];
            a.Initialize();

            for (int i = 0; i < channelCount; i++)
            {
                res[i] = new Image<Gray, D>(Width, Height);
                a[i] = res[i].Ptr;
            }

            CvInvoke.cvSplit(Ptr, a[0], a[1], a[2], a[3]);

            return res;
        }

        ///<summary> Find the edges on this image and marked them in the returned image.</summary>
        ///<param name="thresh"> The threshhold to find initial segments of strong edges</param>
        ///<param name="threshLinking"> The threshold used for edge Linking</param>
        ///<returns> The edges found by the Canny edge detector</returns>
        public Image<C, D> Canny(C thresh, C threshLinking)
        {
            Image<C, D> res = BlankClone();
            double[] t1 = thresh.Coordinate;
            double[] t2 = threshLinking.Coordinate;
            Emgu.Utils.Action<IntPtr, IntPtr, int> act =
                delegate(IntPtr src, IntPtr dest, int channel)
                {
                    CvInvoke.cvCanny(src, dest, t1[channel], t2[channel], 3);
                };
            ForEachChannel<D>(act, res);

            return res;
        }

        ///<summary> Use impaint to recover the intensity of the pixels which location defined by <paramref>mask</paramref> on <i>this</i> image </summary>
        ///<returns> The inpainted image </returns>
        public Image<C, D> InPaint(Image<Gray, Byte> mask, double radius)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvInpaint(Ptr, mask.Ptr, res.Ptr, CvEnum.INPAINT_TYPE.CV_INPAINT_TELEA, radius);
            return res;
        }

        ///<summary> Return a filpped copy of the current image</summary>
        ///<param name="flipType">The type of the flipping</param>
        ///<returns> The flipped copy of <i>this</i> image </returns>
        public Image<C, D> Flip(CvEnum.FLIP flipType)
        {
            int code = 0; //flipType == Emgu.CV.CvEnum.FLIP.VERTICAL

            if (flipType == (Emgu.CV.CvEnum.FLIP.HORIZONTAL | Emgu.CV.CvEnum.FLIP.VERTICAL)) code = -1;
            else if (flipType == Emgu.CV.CvEnum.FLIP.HORIZONTAL) code = 1;

            Image<C, D> res = BlankClone();
            CvInvoke.cvFlip(Ptr, res.Ptr, code);
            return res;
        }

        /// <summary>
        /// Find a sequence of contours
        /// </summary>
        /// <param name="method">The type of approximation method</param>
        /// <param name="type">The retrival type</param>
        /// <param name="stor">The storage used by the sequences</param>
        /// <returns>A sequence of CvContours</returns>
        public Contour FindContours(CvEnum.CHAIN_APPROX_METHOD method, CvEnum.RETR_TYPE type, MemStorage stor)
        {
            IntPtr seq = IntPtr.Zero;
            using (Image<C, D> imagecopy = Clone()) //since cvFindContours modifies the content of the source, we need to make a clone
            {
                CvInvoke.cvFindContours(
                    imagecopy.Ptr,
                    stor.Ptr,
                    ref seq,
                    Marshal.SizeOf(typeof(MCvContour)),
                    type,
                    method,
                    new MCvPoint(0, 0));
            }
            return new Contour(seq, stor);
        }

        ///<summary>
        ///Erodes <i>this</i> image using a 3x3 rectangular structuring element.
        ///Erosion are applied serveral (iterations) times
        ///</summary>
        ///<returns> The eroded image</returns>
        public Image<C, D> Erode(int iterations)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvErode(Ptr, res.Ptr, IntPtr.Zero, iterations);
            return res;
        }

        ///<summary>
        ///Dilates <i>this</i> image using a 3x3 rectangular structuring element.
        ///Dilation are applied serveral (iterations) times
        ///</summary>
        ///<returns> The dialated image</returns>
        public Image<C, D> Dilate(int iterations)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvDilate(Ptr, res.Ptr, IntPtr.Zero, iterations);
            return res;
        }

        ///<summary>Checks that image elements lie between two scalars</summary>
        ///<param name="lower"> The lower limit of color value</param>
        ///<param name="higher"> The upper limit of color value</param>
        ///<returns> res[i,j] = 255 if inrange, 0 otherwise</returns>
        public Image<C, Byte> InRange(C lower, C higher)
        {
            Image<C, Byte> res = new Image<C, Byte>(Width, Height);
            CvInvoke.cvInRangeS(Ptr, lower.CvScalar, higher.CvScalar, res.Ptr);
            return res;
        }

        /// <summary>
        /// Raises every element of input array to p
        /// dst(I)=src(I)^p, if p is integer
        /// dst(I)=abs(src(I))^p, otherwise
        /// </summary>
        /// <param name="power">The exponent of power</param>
        /// <returns>The power image</returns>
        public Image<C, D> Pow(double power)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvPow(Ptr, res.Ptr, power);
            return res;
        }

        /// <summary>
        /// calculates exponent of every element of input array:
        /// dst(I)=exp(src(I))
        /// Maximum relative error is ≈7e-6. Currently, the function converts denormalized values to zeros on output.
        /// </summary>
        /// <returns>The exponent image</returns>
        public Image<C, D> Exp()
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvExp(Ptr, res.Ptr);
            return res;
        }

        /// <summary>
        /// performs forward or inverse transform of 1D or 2D floating-point array
        /// </summary>
        /// <param name="type">Transformation flags</param>
        /// <param name="nonzero_rows">Number of nonzero rows to in the source array (in case of forward 2d transform), or a number of rows of interest in the destination array (in case of inverse 2d transform). If the value is negative, zero, or greater than the total number of rows, it is ignored. The parameter can be used to speed up 2d convolution/correlation when computing them via DFT</param>
        /// <returns>The result of DFT</returns>
        public Image<C, Single> DFT(CvEnum.CV_DXT type, int nonzero_rows)
        {
            Image<C, Single> res = new Image<C, float>(Width, Height);
            CvInvoke.cvDFT(Ptr, res.Ptr, type, nonzero_rows);
            return res;
        }

        /// <summary>
        /// performs forward or inverse transform of 2D floating-point image
        /// </summary>
        /// <param name="type">Transformation flags</param>
        /// <returns>The result of DFT</returns>
        public Image<C, Single> DFT(CvEnum.CV_DXT type)
        {
            return DFT(type, 0);
        }

        /// <summary>
        /// performs forward or inverse transform of 2D floating-point image
        /// </summary>
        /// <param name="type">Transformation flags</param>
        /// <returns>The result of DCT</returns>
        public Image<C, Single> DCT(CvEnum.CV_DCT_TYPE type)
        {
            Image<C, Single> res = new Image<C, float>(Width, Height);
            CvInvoke.cvDCT(Ptr, res.Ptr, type);
            return res;
        }

        /// <summary>
        /// Calculates natural logarithm of absolute value of every element of input array
        /// </summary>
        /// <returns>Natural logarithm of absolute value of every element of input array</returns>
        public Image<C, D> Log()
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvLog(Ptr, res.Ptr);
            return res;
        }

        ///<summary> 
        ///Computes absolute different between <i>this</i> image and the other image
        ///</summary>
        ///<param name="img2">The other image to compute absolute different with</param>
        ///<returns> The image that contains the absolute different value</returns>
        public Image<C, D> AbsDiff(Image<C, D> img2)
        {
            Image<C, D> res = BlankClone();
            CvInvoke.cvAbsDiff(Ptr, img2.Ptr, res.Ptr);
            return res;
        }

        /// <summary>
        /// This function compare the current image with <paramref name="img2"/> and returns the comparison mask
        /// </summary>
        /// <param name="img2">The other image to compare with</param>
        /// <param name="cmp_type">The comparison type</param>
        /// <returns>The result of the comparison as a mask</returns>
        public Image<C, Byte> Cmp(Image<C, D> img2, CvEnum.CMP_TYPE cmp_type)
        {
            Image<C, Byte> res = new Image<C, byte>(Width, Height);

            int dimension = Color.Dimension;
            if (dimension == 1)
            {
                CvInvoke.cvCmp(Ptr, img2.Ptr, res.Ptr, cmp_type);
            }
            else
            {
                using (Image<Gray, D> src1 = new Image<Gray, D>(Width, Height))
                using (Image<Gray, D> src2 = new Image<Gray, D>(Width, Height))
                using (Image<Gray, D> dest = new Image<Gray, D>(Width, Height))
                    for (int i = 0; i < dimension; i++)
                    {
                        CvInvoke.cvSetImageCOI(Ptr, i + 1);
                        CvInvoke.cvSetImageCOI(img2.Ptr, i + 1);
                        CvInvoke.cvCopy(Ptr, src1.Ptr, IntPtr.Zero);
                        CvInvoke.cvCopy(img2.Ptr, src2.Ptr, IntPtr.Zero);

                        CvInvoke.cvCmp(src1.Ptr, src2.Ptr, dest.Ptr, cmp_type);

                        CvInvoke.cvSetImageCOI(res.Ptr, i + 1);
                        CvInvoke.cvCopy(dest.Ptr, res.Ptr, IntPtr.Zero);
                    }
                CvInvoke.cvSetImageCOI(Ptr, 0);
                CvInvoke.cvSetImageCOI(img2.Ptr, 0);
                CvInvoke.cvSetImageCOI(res.Ptr, 0);
            }

            return res;
        }

        /// <summary>
        /// This function compare the current image with <paramref name="value"/> and returns the comparison mask
        /// </summary>
        /// <param name="value">The value to compare with</param>
        /// <param name="cmp_type">The comparison type</param>
        /// <returns>The result of the comparison as a mask</returns>
        public Image<C, Byte> Cmp(double value, CvEnum.CMP_TYPE cmp_type)
        {
            Image<C, Byte> res = new Image<C, byte>(Width, Height);

            int dimension = Color.Dimension;
            if (dimension == 1)
            {
                CvInvoke.cvCmpS(Ptr, value, res.Ptr, cmp_type);
            }
            else
            {
                using (Image<Gray, D> src1 = new Image<Gray, D>(Width, Height))
                using (Image<Gray, D> dest = new Image<Gray, D>(Width, Height))
                    for (int i = 0; i < dimension; i++)
                    {
                        CvInvoke.cvSetImageCOI(Ptr, i + 1);
                        CvInvoke.cvCopy(Ptr, src1.Ptr, IntPtr.Zero);

                        CvInvoke.cvCmpS(src1.Ptr, value, dest.Ptr, cmp_type);

                        CvInvoke.cvSetImageCOI(res.Ptr, i + 1);
                        CvInvoke.cvCopy(dest.Ptr, res.Ptr, IntPtr.Zero);
                    }
                CvInvoke.cvSetImageCOI(Ptr, 0);
                CvInvoke.cvSetImageCOI(res.Ptr, 0);
            }

            return res;
        }


        /// <summary>
        /// Compare two images, returns true if the each of the pixels are equal, false otherwise
        /// </summary>
        /// <param name="img2">The other image to compare with</param>
        /// <returns>true if the each of the pixels for the two images are equal, false otherwise</returns>
        public bool Equals(Image<C, D> img2)
        {
            if (!EqualSize(img2)) return false;

            using (Image<C, Byte> neqMask = Cmp(img2, Emgu.CV.CvEnum.CMP_TYPE.CV_CMP_NE))
            {
                return (neqMask.Sum.Norm == 0.0);
            }
        }

        #region generic operations
        ///<summary> perform an generic action based on each element of the Image</summary>
        public void Action(System.Action<D> action)
        {
            MIplImage image1 = MIplImage;
            int data1 = image1.imageData.ToInt32();
            int step1 = image1.widthStep;
            int cols1 = image1.width * image1.nChannels;

            int sizeOfD = Marshal.SizeOf(typeof(D));
            int width1 = sizeOfD * cols1;
            if (image1.roi != IntPtr.Zero)
            {
                Rectangle<double> rec = ROI;
                data1 += (int)rec.Bottom * step1
                        + sizeOfD * (int)rec.Left * image1.nChannels;
            }

            D[] row1 = new D[cols1];
            GCHandle handle1 = GCHandle.Alloc(row1, GCHandleType.Pinned);
            for (int row = 0; row < Height; row++, data1 += step1)
            {
                Emgu.Utils.memcpy(handle1.AddrOfPinnedObject(), new IntPtr(data1), width1);
                System.Array.ForEach(row1, action);
            }
            handle1.Free();
        }

        /// <summary>
        /// perform an generic operation based on the elements of the two images
        /// </summary>
        /// <typeparam name="D2">The depth of the second image</typeparam>
        /// <param name="img2">The second image to perform action on</param>
        /// <param name="action">An action such that the first parameter is the a single channel of a pixel from the first image, the second parameter is the corresponding channel of the correspondind pixel from the second image </param>
        public void Action<D2>(Image<C, D2> img2, Emgu.Utils.Action<D, D2> action)
        {
            Debug.Assert(EqualSize(img2));

            int data1, height1, cols1, width1, step1;
            RoiParam(Ptr, out data1, out height1, out cols1, out width1, out step1);

            int data2, height2, cols2, width2, step2;
            RoiParam(img2.Ptr, out data2, out height2, out cols2, out width2, out step2);

            D[] row1 = new D[cols1];
            D2[] row2 = new D2[cols1];
            GCHandle handle1 = GCHandle.Alloc(row1, GCHandleType.Pinned);
            GCHandle handle2 = GCHandle.Alloc(row2, GCHandleType.Pinned);

            for (int row = 0; row < height1; row++, data1 += step1, data2 += step2)
            {
                Emgu.Utils.memcpy(handle1.AddrOfPinnedObject(), (IntPtr)data1, width1);
                Emgu.Utils.memcpy(handle2.AddrOfPinnedObject(), (IntPtr)data2, width2);
                for (int col = 0; col < cols1; action(row1[col], row2[col]), col++) ;
            }
            handle1.Free();
            handle2.Free();
        }

        ///<summary> Compute the element of a new image based on the value as well as the x and y positions of each pixel on the image</summary> 
        public Image<C, D2> Convert<D2>(Emgu.Utils.Converter<D, int, int, D2> converter)
        {
            Image<C, D2> res = new Image<C, D2>(Width, Height);

            int nchannel = MIplImage.nChannels;

            int data1, height1, cols1, width1, step1;
            RoiParam(Ptr, out data1, out height1, out cols1, out width1, out step1);

            int data2, height2, cols2, width2, step2;
            RoiParam(res.Ptr, out data2, out height2, out cols2, out width2, out step2);

            D[] row1 = new D[cols1];
            D2[] row2 = new D2[cols1];
            GCHandle handle1 = GCHandle.Alloc(row1, GCHandleType.Pinned);
            GCHandle handle2 = GCHandle.Alloc(row2, GCHandleType.Pinned);

            for (int row = 0; row < height1; row++, data1 += step1, data2 += step2)
            {
                Emgu.Utils.memcpy(handle1.AddrOfPinnedObject(), (IntPtr)data1, width1);
                for (int col = 0; col < cols1; row2[col] = converter(row1[col], row, col / nchannel), col++) ;
                Emgu.Utils.memcpy((IntPtr)data2, handle2.AddrOfPinnedObject(), width2);
            }
            handle1.Free();
            handle2.Free();
            return res;
        }

        ///<summary> Compute the element of the new image based on element of this image</summary> 
        public Image<C, D2> Convert<D2>(System.Converter<D, D2> converter)
        {
            Image<C, D2> res = new Image<C, D2>(Width, Height);

            int data1, height1, cols1, width1, step1;
            RoiParam(Ptr, out data1, out height1, out cols1, out width1, out step1);

            int data2, height2, cols2, width2, step2;
            RoiParam(res.Ptr, out data2, out height2, out cols2, out width2, out step2);

            D[] row1 = new D[cols1];
            D2[] row2 = new D2[cols1];

            GCHandle handle1 = GCHandle.Alloc(row1, GCHandleType.Pinned);
            GCHandle handle2 = GCHandle.Alloc(row2, GCHandleType.Pinned);
            for (int row = 0; row < height1; row++, data1 += step1, data2 += step2)
            {
                Emgu.Utils.memcpy(handle1.AddrOfPinnedObject(), (IntPtr)data1, width1);
                for (int col = 0; col < cols1; row2[col] = converter(row1[col]), col++) ;
                Emgu.Utils.memcpy((IntPtr)data2, handle2.AddrOfPinnedObject(), width2);
            }
            handle1.Free();
            handle2.Free();
            return res;
        }

        ///<summary> Compute the element of the new image based on the elements of the two image</summary>
        public Image<C, D3> Convert<D2, D3>(Image<C, D2> img2, Emgu.Utils.Converter<D, D2, D3> converter)
        {
            if (!EqualSize(img2))
                throw new Emgu.Exception(Emgu.ExceptionHeader.CriticalException, "Image size do not match");

            Image<C, D3> res = new Image<C, D3>(Width, Height);

            int data1, height1, cols1, width1, step1;
            RoiParam(Ptr, out data1, out height1, out cols1, out width1, out step1);

            int data2, height2, cols2, width2, step2;
            RoiParam(img2.Ptr, out data2, out height2, out cols2, out width2, out step2);

            int data3, height3, cols3, width3, step3;
            RoiParam(res.Ptr, out data3, out height3, out cols3, out width3, out step3);

            D[] row1 = new D[cols1];
            D2[] row2 = new D2[cols1];
            D3[] row3 = new D3[cols1];
            GCHandle handle1 = GCHandle.Alloc(row1, GCHandleType.Pinned);
            GCHandle handle2 = GCHandle.Alloc(row2, GCHandleType.Pinned);
            GCHandle handle3 = GCHandle.Alloc(row3, GCHandleType.Pinned);

            for (int row = 0; row < height1; row++, data1 += step1, data2 += step2, data3 += step3)
            {
                Emgu.Utils.memcpy(handle1.AddrOfPinnedObject(), (IntPtr)data1, width1);
                Emgu.Utils.memcpy(handle2.AddrOfPinnedObject(), (IntPtr)data2, width2);
                for (int col = 0; col < cols1; row3[col] = converter(row1[col], row2[col]), col++) ;
                Emgu.Utils.memcpy((IntPtr)data3, handle3.AddrOfPinnedObject(), width3);
            }

            handle1.Free();
            handle2.Free();
            handle3.Free();

            return res;
        }

        ///<summary> Compute the element of the new image based on the elements of the three image</summary>
        public Image<C, D4> Convert<D2, D3, D4>(Image<C, D2> img2, Image<C, D3> img3, Emgu.Utils.Converter<D, D2, D3, D4> converter)
        {
            if (!EqualSize(img2) || !EqualSize(img3))
                throw new Emgu.Exception(Emgu.ExceptionHeader.CriticalException, "Image size do not match");

            Image<C, D4> res = new Image<C, D4>(Width, Height);

            int data1, height1, cols1, width1, step1;
            RoiParam(Ptr, out data1, out height1, out cols1, out width1, out step1);

            int data2, height2, cols2, width2, step2;
            RoiParam(img2.Ptr, out data2, out height2, out cols2, out width2, out step2);

            int data3, height3, cols3, width3, step3;
            RoiParam(img3.Ptr, out data3, out height3, out cols3, out width3, out step3);

            int data4, height4, cols4, width4, step4;
            RoiParam(res.Ptr, out data4, out height4, out cols4, out width4, out step4);

            D[] row1 = new D[cols1];
            D2[] row2 = new D2[cols1];
            D3[] row3 = new D3[cols1];
            D4[] row4 = new D4[cols1];
            GCHandle handle1 = GCHandle.Alloc(row1, GCHandleType.Pinned);
            GCHandle handle2 = GCHandle.Alloc(row2, GCHandleType.Pinned);
            GCHandle handle3 = GCHandle.Alloc(row3, GCHandleType.Pinned);
            GCHandle handle4 = GCHandle.Alloc(row4, GCHandleType.Pinned);

            for (int row = 0; row < height1; row++, data1 += step1, data2 += step2, data3 += step3, data4 += step4)
            {
                Emgu.Utils.memcpy(handle1.AddrOfPinnedObject(), (IntPtr)data1, width1);
                Emgu.Utils.memcpy(handle2.AddrOfPinnedObject(), (IntPtr)data2, width2);
                Emgu.Utils.memcpy(handle3.AddrOfPinnedObject(), (IntPtr)data3, width3);

                for (int col = 0; col < cols1; row4[col] = converter(row1[col], row2[col], row3[col]), col++) ;

                Emgu.Utils.memcpy((IntPtr)data4, handle4.AddrOfPinnedObject(), width4);
            }
            handle1.Free();
            handle2.Free();
            handle3.Free();
            handle4.Free();

            return res;
        }

        ///<summary> Compute the element of the new image based on the elements of the four image</summary>
        public Image<C, D5> Convert<D2, D3, D4, D5>(Image<C, D2> img2, Image<C, D3> img3, Image<C, D4> img4, Emgu.Utils.Converter<D, D2, D3, D4, D5> converter)
        {
            if (!EqualSize(img2) || !EqualSize(img3) || !EqualSize(img4))
                throw new Emgu.Exception(Emgu.ExceptionHeader.CriticalException, "Image size do not match");

            Image<C, D5> res = new Image<C, D5>(Width, Height);

            int data1, height1, cols1, width1, step1;
            RoiParam(Ptr, out data1, out height1, out cols1, out width1, out step1);

            int data2, height2, cols2, width2, step2;
            RoiParam(img2.Ptr, out data2, out height2, out cols2, out width2, out step2);

            int data3, height3, cols3, width3, step3;
            RoiParam(img3.Ptr, out data3, out height3, out cols3, out width3, out step3);

            int data4, height4, cols4, width4, step4;
            RoiParam(img4.Ptr, out data4, out height4, out cols4, out width4, out step4);

            int data5, height5, cols5, width5, step5;
            RoiParam(res.Ptr, out data5, out height5, out cols5, out width5, out step5);

            D[] row1 = new D[cols1];
            D2[] row2 = new D2[cols1];
            D3[] row3 = new D3[cols1];
            D4[] row4 = new D4[cols1];
            D5[] row5 = new D5[cols1];
            GCHandle handle1 = GCHandle.Alloc(row1, GCHandleType.Pinned);
            GCHandle handle2 = GCHandle.Alloc(row2, GCHandleType.Pinned);
            GCHandle handle3 = GCHandle.Alloc(row3, GCHandleType.Pinned);
            GCHandle handle4 = GCHandle.Alloc(row4, GCHandleType.Pinned);
            GCHandle handle5 = GCHandle.Alloc(row5, GCHandleType.Pinned);

            for (int row = 0; row < height1; row++, data1 += step1, data2 += step2, data3 += step3, data4 += step4, data5 += step5)
            {
                Emgu.Utils.memcpy(handle1.AddrOfPinnedObject(), (IntPtr)data1, width1);
                Emgu.Utils.memcpy(handle2.AddrOfPinnedObject(), (IntPtr)data2, width2);
                Emgu.Utils.memcpy(handle3.AddrOfPinnedObject(), (IntPtr)data3, width3);
                Emgu.Utils.memcpy(handle4.AddrOfPinnedObject(), (IntPtr)data4, width4);

                for (int col = 0; col < cols1; row5[col] = converter(row1[col], row2[col], row3[col], row4[col]), col++) ;
                Emgu.Utils.memcpy((IntPtr)data5, handle5.AddrOfPinnedObject(), width5);
            }
            handle1.Free();
            handle2.Free();
            handle3.Free();
            handle4.Free();
            handle5.Free();

            return res;
        }
        #endregion

        #region Implment UnmanagedObject interface
        /// <summary>
        /// Release all unmanaged memory associate with the image
        /// </summary>
        protected override void FreeUnmanagedObjects()
        {
            CvInvoke.cvReleaseImage(ref _ptr);
        }
        #endregion

        #region Operator overload

        /// <summary>
        /// Perform an elementwise AND operation on the two images
        /// </summary>
        /// <param name="img1">The first image to AND</param>
        /// <param name="img2">The second image to AND</param>
        /// <returns>The result of the AND operation</returns>
        public static Image<C, D> operator &(Image<C, D> img1, Image<C, D> img2)
        {
            return img1.And(img2);
        }

        /// <summary>
        /// Perform an elementwise AND operation using an images and a color
        /// </summary>
        /// <param name="img1">The first image to AND</param>
        /// <param name="val">The color to AND</param>
        /// <returns>The result of the AND operation</returns>
        public static Image<C, D> operator &(Image<C, D> img1, double val)
        {
            C color = new C();
            color.CvScalar = new MCvScalar(val, val, val, val);
            return img1.And(color);
        }

        /// <summary>
        /// Perform an elementwise AND operation using an images and a color
        /// </summary>
        /// <param name="img1">The first image to AND</param>
        /// <param name="val">The color to AND</param>
        /// <returns>The result of the AND operation</returns>
        public static Image<C, D> operator &(double val, Image<C, D> img1)
        {
            C color = new C();
            color.CvScalar = new MCvScalar(val, val, val, val);
            return img1.And(color);
        }

        /// <summary>
        /// Perform an elementwise AND operation using an images and a color
        /// </summary>
        /// <param name="img1">The first image to AND</param>
        /// <param name="val">The color to AND</param>
        /// <returns>The result of the AND operation</returns>
        public static Image<C, D> operator &(Image<C, D> img1, C val)
        {
            return img1.And(val);
        }

        /// <summary>
        /// Perform an elementwise AND operation using an images and a color
        /// </summary>
        /// <param name="img1">The first image to AND</param>
        /// <param name="val">The color to AND</param>
        /// <returns>The result of the AND operation</returns>
        public static Image<C, D> operator &(C val, Image<C, D> img1)
        {
            return img1.And(val);
        }

        ///<summary> Perform an elementwise OR operation with another image and return the result</summary>
        ///<returns> The result of the OR operation</returns>
        public static Image<C, D> operator |(Image<C, D> img1, Image<C, D> img2)
        {
            return img1.Or(img2);
        }

        ///<summary> 
        /// Perform an binary OR operation with some color
        /// </summary>
        ///<param name="img1">The image to OR</param>
        ///<param name="val"> The color to OR</param>
        ///<returns> The result of the OR operation</returns>
        public static Image<C, D> operator |(Image<C, D> img1, double val)
        {
            C color = new C();
            color.CvScalar = new MCvScalar(val, val, val, val);
            return img1.Or(color);
        }

        ///<summary> 
        /// Perform an binary OR operation with some color
        /// </summary>
        ///<param name="img1">The image to OR</param>
        ///<param name="val"> The color to OR</param>
        ///<returns> The result of the OR operation</returns>
        public static Image<C, D> operator |(double val, Image<C, D> img1)
        {
            return img1 | val;
        }

        ///<summary> 
        /// Perform an binary OR operation with some color
        /// </summary>
        ///<param name="img1">The image to OR</param>
        ///<param name="val"> The color to OR</param>
        ///<returns> The result of the OR operation</returns>
        public static Image<C, D> operator |(Image<C, D> img1, C val)
        {
            return img1.Or(val);
        }

        ///<summary> 
        /// Perform an binary OR operation with some color
        /// </summary>
        ///<param name="img1">The image to OR</param>
        ///<param name="val"> The color to OR</param>
        ///<returns> The result of the OR operation</returns>
        public static Image<C, D> operator |(C val, Image<C, D> img1)
        {
            return img1.Or(val);
        }

        ///<summary> Compute the complement image</summary>
        public static Image<C, D> operator ~(Image<C, D> img1)
        {
            return img1.Not();
        }

        /// <summary>
        /// Elementwise add <paramref name="img1"/> with <paramref name="img2"/>
        /// </summary>
        /// <param name="img1">The first image to be added</param>
        /// <param name="img2">The second image to be added</param>
        /// <returns>The sum of the two images</returns>
        public static Image<C, D> operator +(Image<C, D> img1, Image<C, D> img2)
        {
            return img1.Add(img2);
        }

        /// <summary>
        /// Elementwise add <paramref name="img1"/> with <paramref name="val"/>
        /// </summary>
        /// <param name="img1">The image to be added</param>
        /// <param name="val">The value to be added</param>
        /// <returns>The images plus the color</returns>
        public static Image<C, D> operator +(double val, Image<C, D> img1)
        {
            return img1 + val;
        }

        /// <summary>
        /// Elementwise add <paramref name="img1"/> with <paramref name="val"/>
        /// </summary>
        /// <param name="img1">The image to be added</param>
        /// <param name="val">The value to be added</param>
        /// <returns>The images plus the color</returns>
        public static Image<C, D> operator +(Image<C, D> img1, double val)
        {
            C color = new C();
            color.CvScalar = new MCvScalar(val, val, val, val);
            return img1.Add(color);
        }

        /// <summary>
        /// Elementwise add <paramref name="img1"/> with <paramref name="val"/>
        /// </summary>
        /// <param name="img1">The image to be added</param>
        /// <param name="val">The color to be added</param>
        /// <returns>The images plus the color</returns>
        public static Image<C, D> operator +(Image<C, D> img1, C val)
        {
            return img1.Add(val);
        }

        /// <summary>
        /// Elementwise add <paramref name="img1"/> with <paramref name="val"/>
        /// </summary>
        /// <param name="img1">The image to be added</param>
        /// <param name="val">The color to be added</param>
        /// <returns>The images plus the color</returns>
        public static Image<C, D> operator +(C val, Image<C, D> img1)
        {
            return img1.Add(val);
        }

        /// <summary>
        /// Elementwise subtract another image from the current image
        /// </summary>
        /// <param name="img1">The image to be substracted</param>
        /// <param name="img2">The second image to be subtraced from <paramref name="img1"/></param>
        /// <returns> The result of elementwise subtracting img2 from <paramref name="img1"/> </returns>
        public static Image<C, D> operator -(Image<C, D> img1, Image<C, D> img2)
        {
            return img1.Sub(img2);
        }

        /// <summary>
        /// Elementwise subtract another image from the current image
        /// </summary>
        /// <param name="img1">The image to be substracted</param>
        /// <param name="val">The color to be subtracted</param>
        /// <returns> The result of elementwise subtracting <paramred name="val"/> from <paramref name="img1"/> </returns>
        public static Image<C, D> operator -(Image<C, D> img1, C val)
        {
            return img1.Sub(val);
        }

        /// <summary>
        /// Elementwise subtract another image from the current image
        /// </summary>
        /// <param name="img1">The image to be substracted</param>
        /// <param name="val">The color to be subtracted</param>
        /// <returns> <paramred name="val"/> - <paramref name="img1"/> </returns>
        public static Image<C, D> operator -(C val, Image<C, D> img1)
        {
            return img1.SubR(val);
        }

        /// <summary>
        /// <paramred name="val"/> - <paramref name="img1"/>
        /// </summary>
        /// <param name="img1">The image to be substracted</param>
        /// <param name="val">The value to be subtracted</param>
        /// <returns> <paramred name="val"/> - <paramref name="img1"/> </returns>
        public static Image<C, D> operator -(double val, Image<C, D> img1)
        {
            C color = new C();
            color.CvScalar = new MCvScalar(val, val, val, val);
            return img1.SubR(color);
        }

        /// <summary>
        /// Elementwise subtract another image from the current image
        /// </summary>
        /// <param name="img1">The image to be substracted</param>
        /// <param name="val">The value to be subtracted</param>
        /// <returns> <paramref name="img1"/> - <paramred name="val"/>   </returns>
        public static Image<C, D> operator -(Image<C, D> img1, double val)
        {
            C color = new C();
            color.CvScalar = new MCvScalar(val, val, val, val);
            return img1.Sub(color);
        }

        /// <summary>
        ///  <paramref name="img1"/> * <paramref name="scale"/>
        /// </summary>
        /// <param name="img1">The image</param>
        /// <param name="scale">The multiplication scale</param>
        /// <returns><paramref name="img1"/> * <paramref name="scale"/></returns>
        public static Image<C, D> operator *(Image<C, D> img1, double scale)
        {
            return img1.Mul(scale);
        }

        /// <summary>
        ///   <paramref name="scale"/>*<paramref name="img1"/>
        /// </summary>
        /// <param name="img1">The image</param>
        /// <param name="scale">The multiplication scale</param>
        /// <returns><paramref name="scale"/>*<paramref name="img1"/></returns>
        public static Image<C, D> operator *(double scale, Image<C, D> img1)
        {
            return img1.Mul(scale);
        }

        /// <summary>
        /// Perform the convolution with <paramref name="kernel"/> on <paramref name="img1"/>
        /// </summary>
        /// <param name="img1">The image</param>
        /// <param name="kernel">The kernel</param>
        /// <returns>Result of the convolution</returns>
        public static Image<C, Single> operator *(Image<C, D> img1, ConvolutionKernelF kernel)
        {
            return img1.Convolution(kernel);
        }

        /// <summary>
        ///  <paramref name="img1"/> / <paramref name="scale"/>
        /// </summary>
        /// <param name="img1">The image</param>
        /// <param name="scale">The division scale</param>
        /// <returns><paramref name="img1"/> / <paramref name="scale"/></returns>
        public static Image<C, D> operator /(Image<C, D> img1, double scale)
        {
            return img1.Mul(1.0 / scale);
        }

        /// <summary>
        ///   <paramref name="scale"/> / <paramref name="img1"/>
        /// </summary>
        /// <param name="img1">The image</param>
        /// <param name="scale">The scale</param>
        /// <returns><paramref name="scale"/> / <paramref name="img1"/></returns>
        public static Image<C, D> operator /(double scale, Image<C, D> img1)
        {
            Image<C, D> res = img1.BlankClone();
            CvInvoke.cvDiv(IntPtr.Zero, img1.Ptr, res.Ptr, scale);
            return res;
        }

        #endregion
    }
}
