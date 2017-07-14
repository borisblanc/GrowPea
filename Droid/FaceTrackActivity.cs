using Android.App;
using Android.Widget;
using Android.OS;
using Android.Gms.Vision;
using Android.Util;
using Android.Gms.Vision.Faces;
using System;
using System.Collections.Generic;
using Android.Graphics;
using Android.Gms.Common;
using Android.Content.PM;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Views;
using Java.IO;
using Java.Nio;
using Console = System.Console;
using Face = Android.Gms.Vision.Faces.Face;
using File = System.IO.File;


namespace GrowPea.Droid
{
    [Activity(Label = "FaceTrackActivity", ScreenOrientation = ScreenOrientation.Landscape)]
    public class FaceTrackActivity : Activity
    {
        private static readonly string TAG = "FaceTrackActivity";


        private GraphicOverlay mGraphicOverlay;

        private Button mRecbutton;
        private ImageButton mSwitchcamButton;
        private Button mPlaybutton;


        private bool isRecording = false;

        private int pFramewidth = 640; //eventually should be user driven and based on device capabilities
        private int pFrameHeight = 360; //eventually should be user driven and based on device capabilities

        private Single _recordFps = 60.0f;
        private int _createfps = 30;
        private int vidlengthseconds = 3;
        private int minframescount = 200;


        private CameraFacing camface = CameraFacing.Front; //default may change

        private string _currentfilepath;

        public SortedList<long, SparseArray> _framelist;

        private static readonly Java.IO.File _downloadsfilesdir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);

        private static readonly Java.IO.File _camerafilesdir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDcim);

        private static string _inputfilename = "MVI_3057.MOV";//"636354234996333350.mp4";//"VID_20170713_174052.mp4"; "MVI_3056.MOV"


        protected override void OnCreate(Bundle bundle)
        {
            try
            {
                base.OnCreate(bundle);
                RequestWindowFeature(WindowFeatures.NoTitle);

                Window.AddFlags(WindowManagerFlags.Fullscreen);

                // Set our view from the "main" layout resource
                SetContentView(Resource.Layout.VideoCapture);

                mGraphicOverlay = FindViewById<GraphicOverlay>(Resource.Id.faceOverlay);
                mRecbutton = FindViewById<Button>(Resource.Id.btnRecord);
                mSwitchcamButton = FindViewById<ImageButton>(Resource.Id.btnswCam);
                mPlaybutton = FindViewById<Button>(Resource.Id.btnPlay);

                mRecbutton.Click += (sender, e) => ToggleRecording();
                mSwitchcamButton.Click += (sender, e) => ToggleCamface();
                mPlaybutton.Click += (sender, e) => OpenVideo();
                mPlaybutton.Enabled = _currentfilepath != null;


                _framelist = new SortedList<long, SparseArray>();
                Stopwatch s = new Stopwatch();
                s.Start();

                new ExtractMpegFrames(_downloadsfilesdir, _inputfilename, ref _framelist, pFramewidth, pFrameHeight);

                if (_framelist.Count > minframescount)
                {
                    StartFrameProcessing(s);
                }
                else
                {
                    Showpopup("Not enough frames recorded try again!", ToastLength.Short);
                }
                          
            }
            catch (Exception e)
            {

            }
        }


        private void Performanceresults(Stopwatch s)
        {
            s.Stop();
            Log.Info("innerSTOPWATCH!!!!:", s.ElapsedMilliseconds.ToString());
        }

        private async void StartFrameProcessing(Stopwatch s)
        {
            try
            {
                Showpopup("Processing Smiles :)!", ToastLength.Short);

                var fdp = new FrameDataProcessor(ref _framelist, _createfps, vidlengthseconds);
                var besttimestamprange = await fdp.BeginFramesProcess(); //get best frames collection: todo change to best frames indexes instead

                if (besttimestamprange == null)
                {
                    Showpopup("Error with Smiles processing :(!", ToastLength.Short);
                }
                else
                {
                    Showpopup("Smiles processed :)!", ToastLength.Short);

                    Java.IO.File inputFile = new Java.IO.File(_downloadsfilesdir, _inputfilename);
                    var outputfilename = string.Format("{0}.mp4", DateTime.Now.Ticks);
                    Java.IO.File outputFile = new Java.IO.File(_downloadsfilesdir, outputfilename);

                    var result = VideoUtils.startTrim(inputFile, outputFile, besttimestamprange.Item1, besttimestamprange.Item2);

                    if (File.Exists(outputFile.AbsolutePath))
                    {
                        Showpopup("Video Created, Press Play!!!", ToastLength.Short);
                        _currentfilepath = outputFile.AbsolutePath;
                        TogglePlay(true);
                    }
                    else
                    {
                        Showpopup("Error with video(!", ToastLength.Short);
                        _currentfilepath = null;
                        TogglePlay(false);
                    }
                    Performanceresults(s);
                }
 
            }
            catch (Exception e)
            {
                var x = e;

            }

        }

        private void ToggleRecording()
        {
            if (!isRecording)
            {
                isRecording = true;
                mGraphicOverlay.isRecording = true;
                mRecbutton.Text = "STOP";
                mRecbutton.SetTextColor(Color.Red);
                mSwitchcamButton.Enabled = false;

            }
            else
            {
                isRecording = false;
                mGraphicOverlay.isRecording = false;
                mRecbutton.Text = "RECORD";
                mRecbutton.SetTextColor(Color.Black);
                mSwitchcamButton.Enabled = true;

            }
        }



        private void ToggleCamface()
        {
            camface = camface == CameraFacing.Front ? CameraFacing.Back : CameraFacing.Front;
        }


        private void OpenVideo()
        {
            try
            {
                if (_currentfilepath != null && File.Exists(_currentfilepath))
                {
                    var intent = new Intent(this, typeof(VideoViewer));
                    intent.PutExtra("FilePath", _currentfilepath);
                    StartActivity(intent);
                }
                else
                {
                    Showpopup("Video not available", ToastLength.Short);
                }
            }
            catch (Exception e)
            {

            }

        }





        private void TogglePlay(bool toggle)
        {
            this.RunOnUiThread(() =>
            {
                mPlaybutton.Enabled = toggle;
            });
        }

        private void Showpopup(string msg, ToastLength length)
        {
            this.RunOnUiThread(() =>
            {
                var toast = Toast.MakeText(this, msg, length);
                toast.Show();
            });
        }



        /**
   * Starts or restarts the camera source, if it exists.  If the camera source doesn't exist yet
   * (e.g., because onResume was called before the camera source was created), this will be called
   * again when the camera source is created.
   */


        class GraphicFaceTracker : Tracker
        {
            private GraphicOverlay mOverlay;
            private FaceGraphic mFaceGraphic;

            public GraphicFaceTracker(GraphicOverlay overlay)
            {
                mOverlay = overlay;
                mFaceGraphic = new FaceGraphic(overlay);
            }

            public override void OnNewItem(int id, Java.Lang.Object item)
            {
                mFaceGraphic.SetId(id);
            }

            public override void OnUpdate(Detector.Detections detections, Java.Lang.Object item)
            {

                var face = item as Face;
                mOverlay.Add(mFaceGraphic);
                mFaceGraphic.UpdateFace(face);
            }

            public override void OnMissing(Detector.Detections detections)
            {
                mOverlay.Remove(mFaceGraphic);
            }

            public override void OnDone()
            {
                mOverlay.Remove(mFaceGraphic);
            }
        }



    }


}

