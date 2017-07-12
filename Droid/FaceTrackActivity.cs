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

        private int pFramewidth = 1280; //eventually should be user driven and based on device capabilities
        private int pFrameHeight = 720; //eventually should be user driven and based on device capabilities

        private Single _recordFps = 60.0f;
        private int _createfps = 30;
        private int vidlengthseconds = 10;


        private CameraFacing camface = CameraFacing.Front; //default may change

        private string _currentfilepath;

        public Dictionary<double, Face> _framelist;

        private int compressquality = 100; //tested ok for now with lossy jpeg compression default for now byt need to be user configurable

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


                _framelist = new Dictionary<double, Face>();
                new ExtractMpegFrames("636354234996333350.mp4", ref _framelist);
            }
            catch (Exception e)
            {

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

