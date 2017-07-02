using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Content.PM;
using Mindscape.Raygun4Net;
using Mindscape.Raygun4Net.Messages;

namespace GrowPea.Droid
{
    [Activity(Label = "SayCheese", MainLauncher = true, Icon = "@drawable/Cheese", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainMenu : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RaygunClient.Initialize("3amrQKpbXz5tMRZ1yoGgQw==").AttachCrashReporting().AttachPulse(this);

            RaygunClient.Current.UserInfo = new RaygunIdentifierMessage("borisblanc@gmail.com")
            {
                FirstName = "Boris",
                FullName = "BR",
            };

            SetContentView(Resource.Layout.MainMenu);
            // Create your application here

            var btnGrowsaic = FindViewById<Button>(Resource.Id.btn_Growsaic);

            btnGrowsaic.Click += (object sender, EventArgs e) =>
            {
                var intent = new Intent(this, typeof(Growsaic));
                StartActivity(intent);
            };

            //throw new InvalidOperationException("raygun bullshit");
        }
    }
}