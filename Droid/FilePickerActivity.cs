using System;
using Android.Content.PM;
using GrowPea.Droid;

namespace com.xamarin.recipes.filepicker
{
    using Android.App;
    using Android.OS;
    using Android.Support.V4.App;

    [Activity(Label = "FilePicker", ScreenOrientation = ScreenOrientation.Portrait)]
    public class FilePickerActivity : FragmentActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            try
            {
                base.OnCreate(bundle);
                SetContentView(Resource.Layout.File_Main);

                var path = Intent.GetStringExtra("defaultFilePath");

                if (!string.IsNullOrEmpty(path))
                {
                    FileListFragment.DefaultInitialDirectory = path;
                }
            }
            catch (Exception e)
            {
                var x = e;
            }
        }
    }
}
