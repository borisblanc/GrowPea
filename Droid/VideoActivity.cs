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
    [Activity(Label = "VideoActivity", ScreenOrientation = ScreenOrientation.Landscape)]
    public class VideoActivity : Activity
    {

        private static readonly string TAG = "VideoActivity";

        private CameraSource mCameraSource = null;

        private CameraSourcePreview mPreview;
        private GraphicOverlay mGraphicOverlay;

        private Button mRecbutton;
        private ImageButton mSwitchcamButton;
        private Button mPlaybutton;

        private static readonly int RC_HANDLE_GMS = 9001;

        private bool isRecording = false;

        private int pFramewidth = 1280; //eventually should be user driven and based on device capabilities
        private int pFrameHeight = 720; //eventually should be user driven and based on device capabilities

        private Single _recordFps = 60.0f;
        private int _createfps = 30;
        private int vidlengthseconds = 3;

        private const int framemin = 200; //minimum number of frames needed to process this

        private CameraFacing camface = CameraFacing.Front; //default may change

        private string _currentfilepath;

        private SortedList<float, FrameData> _allFrameData;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            this.RequestWindowFeature(WindowFeatures.NoTitle);

            this.Window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
            
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.VideoCapture);

            mPreview = FindViewById<CameraSourcePreview>(Resource.Id.preview);
            mGraphicOverlay = FindViewById<GraphicOverlay>(Resource.Id.faceOverlay);
            mRecbutton = FindViewById<Button>(Resource.Id.btnRecord);
            mSwitchcamButton = FindViewById<ImageButton>(Resource.Id.btnswCam);
            mPlaybutton = FindViewById<Button>(Resource.Id.btnPlay);

            mRecbutton.Click += (sender, e) => ToggleRecording();
            mSwitchcamButton.Click += (sender, e) => ToggleCamface();
            mPlaybutton.Click += (sender, e) => OpenVideo();
            mPlaybutton.Enabled = _currentfilepath != null;

            CreateCameraSource(false);
        }


        private void ToggleRecording()
        {
            if (!isRecording)
            {
                ReleaseRestartResources(true);
                isRecording = true;
                mGraphicOverlay.isRecording = true;
                mRecbutton.Text = "STOP";
                mRecbutton.SetTextColor(Color.Red);
                mSwitchcamButton.Enabled = false;

            }
            else
            {
                ReleaseRestartResources(false);
                isRecording = false;
                mGraphicOverlay.isRecording = false;
                mRecbutton.Text = "RECORD";
                mRecbutton.SetTextColor(Color.Black);
                mSwitchcamButton.Enabled = true;

                StartFrameProcessing(); 
            }
        }

        private void ToggleCamface()
        {
            if (camface == CameraFacing.Front)
            {
                camface = CameraFacing.Back;
                ReleaseRestartResources(false);
            }
            else
            {
                camface = CameraFacing.Front;
                ReleaseRestartResources(false);
            }
        }

        private void ReleaseRestartResources(bool startfaceprocessing)
        {
            mCameraSource.Release();
            mPreview.Stop();
            CreateCameraSource(startfaceprocessing);
            StartCameraSource();
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

        //this should ultimately be user driven like google photos can recycle code below later to find out hardware capabilites when giving users options
        //private void SetVideoSize(CameraFacing camface) //determines cameras supported sizes and sets frame sizes for video as best as possible
        //{
        //    CameraManager manager = (CameraManager)GetSystemService(Context.CameraService);
        //    var cams = manager.GetCameraIdList();

        //    LensFacing lensface = camface == CameraFacing.Back ? LensFacing.Back : LensFacing.Front;

        //    foreach (var camid in cams)
        //    {
        //        var camprops = manager.GetCameraCharacteristics(camid);

        //        if ((int)camprops.Get(CameraCharacteristics.LensFacing) == (int) lensface)
        //        {
        //            var map = (StreamConfigurationMap)camprops.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
        //            Size[] sizes = map.GetOutputSizes((int)ImageFormatType.Jpeg);
        //            if (sizes.ToList().Any(s => s.Width == 1280 && s.Height == 720)) //if this format is supported then always use it, this is 16:9 aspect HD
        //            {
        //                pFramewidth = 1280;
        //                pFrameHeight = 720;
        //            }
        //            else //if (sizes.ToList().Any(s => s.Width == 640 && s.Height == 480)) //if HD is not supported use this default
        //            {//if HD is not supported use this default
        //                pFramewidth = 640;
        //                pFrameHeight = 480;
        //            }
        //        }
        //    }

        //}


        private void CreateCameraSource(bool usecustomdetector)
        {
            //SetVideoSize(camface);

            var context = Application.Context;

            FaceDetector detector = new FaceDetector.Builder(context) //consider moving to background thread
                    .SetTrackingEnabled(true)
                    .SetClassificationType(ClassificationType.All)
                    .SetProminentFaceOnly(true)
                    .SetMinFaceSize((float)0.2)
                    .Build();


            if (usecustomdetector)
            {

                _allFrameData = new SortedList<float, FrameData>();
                var myFaceDetector = new CustomFaceDetector(detector, ref _allFrameData);

                //myFaceDetector.PropertyChanged += OnPropertyChanged;

                myFaceDetector.SetProcessor(
                    new LargestFaceFocusingProcessor.Builder(myFaceDetector,
                            new GraphicFaceTracker(this.mGraphicOverlay))
                        .Build());

                if (!myFaceDetector.IsOperational)
                {
                    // isOperational() can be used to check if the required native library is currently
                    // available.  The detector will automatically become operational once the library
                    // download completes on device.
                    Log.Warn(TAG, "Face detector dependencies are not yet available.");
                }

                mCameraSource = new CameraSource.Builder(context, myFaceDetector)
                    .SetRequestedPreviewSize(pFramewidth, pFrameHeight)
                    .SetFacing(camface)
                    .SetRequestedFps(_recordFps)
                    .SetAutoFocusEnabled(true)
                    .Build();
            }
            else
            {
                detector.SetProcessor(
                    new LargestFaceFocusingProcessor.Builder(detector,
                            new GraphicFaceTracker(this.mGraphicOverlay))
                        .Build());

                if (!detector.IsOperational)
                {
                    // isOperational() can be used to check if the required native library is currently
                    // available.  The detector will automatically become operational once the library
                    // download completes on device.
                    Log.Warn(TAG, "Face detector dependencies are not yet available.");
                }

                mCameraSource = new CameraSource.Builder(context, detector)
                    .SetRequestedPreviewSize(pFramewidth, pFrameHeight)
                    .SetFacing(camface)
                    .SetRequestedFps(_recordFps)
                    .SetAutoFocusEnabled(true)
                    .Build();
            }
        }

        //private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        //{
        //    if (e.PropertyName == "isRecording" && !myFaceDetector.isRecording)
        //    {
        //        isRecording = false;
        //        mGraphicOverlay.isRecording = false;
        //        this.RunOnUiThread(() => //can only do this on UI thread
        //        {
        //            mRecbutton.Text = "RECORD";
        //            mRecbutton.SetTextColor(Color.Black);

        //            Toast toast = Toast.MakeText(this, "Smiles Captured :)!", ToastLength.Short);
        //            toast.Show();
        //        });

        //        //if (myFaceDetector._allFrameData.Count > 0)
        //        //{
        //        //    StartFrameProcessing();
        //        //}
        //    }
        //}

        private async void StartFrameProcessing()
        {
            try
            {
                Showpopup("Processing Smiles :)!", ToastLength.Short);

                if (_allFrameData != null && _allFrameData.Count >= framemin)
                {
                    var fdp = new FrameDataProcessor(ref _allFrameData, pFramewidth, pFrameHeight, _createfps, vidlengthseconds);
                    var images = await fdp.BeginProcessingFrames();

                    if (images == null)
                    {
                        Showpopup("Error with Smiles processing :(!", ToastLength.Short);
                    }
                    else
                    {
                        Showpopup("Smiles processed :)!", ToastLength.Short);
                        var fileresult = await fdp.BeginMakeBufferVideo(images);

                        if (File.Exists(fileresult))
                        {
                            Showpopup("Video Created, Press Play!!!", ToastLength.Short);
                            _currentfilepath = fileresult;
                            mPlaybutton.Enabled = true;
                        }
                        else
                        {
                            Showpopup("Error with video(!", ToastLength.Short);
                            _currentfilepath = null;
                            mPlaybutton.Enabled = false;
                        }
                    }
                }
                else
                {
                    Showpopup("Need a longer video!! Try Again.", ToastLength.Short);
                }
            }
            catch (Exception e)
            {
                
                throw;
            }
        }

        private void Showpopup(string msg, ToastLength length)
        {
            var toast = Toast.MakeText(this, msg, length);
            toast.Show();
        }
            

        protected override void OnResume()
        {
            base.OnResume();
            StartCameraSource();
        }

        protected override void OnPause()
        {
            base.OnPause();
            mPreview.Stop();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (mCameraSource != null)
            {
                mCameraSource.Release();
            }
        }

        /**
   * Starts or restarts the camera source, if it exists.  If the camera source doesn't exist yet
   * (e.g., because onResume was called before the camera source was created), this will be called
   * again when the camera source is created.
   */
        private void StartCameraSource()
        {

            // check that the device has play services available.
            int code = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this.ApplicationContext);
            if (code != ConnectionResult.Success)
            {
                Dialog dlg = GoogleApiAvailability.Instance.GetErrorDialog(this, code, RC_HANDLE_GMS);
                dlg.Show();
            }

            if (mCameraSource != null)
            {
                try
                {
                    mPreview.Start(mCameraSource, mGraphicOverlay);
                }
                catch (System.Exception e)
                {
                    Log.Error(TAG, "Unable to start camera source.", e);
                    mCameraSource.Release();
                    mCameraSource = null;
                }
            }
        }

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

