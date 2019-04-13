using UnityEngine;
using mixpanel;
using System;
using System.Collections.Generic;

#if UNITY_IOS && UNITY_5_0_0
using NotificationServices = UnityEngine.iOS.NotificationServices;
using NotificationType = UnityEngine.iOS.NotificationType;
#endif

public class MixpanelExample : MonoBehaviour
{
    public GUISkin skin;
    bool tokenSent;

    void Update () {
        if (!tokenSent) {
            #if UNITY_IOS && UNITY_5_0_0
            Mixpanel.People.PushDeviceToken = UnityEngine.iOS.NotificationServices.deviceToken;
            #else
            // Mixpanel.people.PushDeviceToken =
            #endif
            tokenSent = true;
        }
    }

    void OnGUI() {
        GUI.skin = this.skin;

        GUILayout.BeginArea(new Rect(Screen.width * 0.3f, Screen.height * 0.35f, Screen.width * 0.4f, Screen.height * 2.5f));

        if (GUILayout.Button("Track"))
        {
            // a simple tracking call
            Mixpanel.Track("The Button Was Clicked");
        }

        if (GUILayout.Button("Engage")) // an engage call
        {
            Mixpanel.People.Increment("clicks", 1);
        }

        if (GUILayout.Button("Opt Out Tracking")) // an engage call
        {
            Mixpanel.OptOutTracking();
        }

        if (GUILayout.Button("Opt In Tracking")) // an engage call
        {
            Mixpanel.OptInTracking();
            // var args1 = new Value();
            // args1["test"] = 100;
            // args1["me"] = 101;
            // Mixpanel.OptInTracking("newDisctinctId2", args1);
        }

        GUILayout.EndArea();
    }

    void Start () {
        tokenSent = false;
        #if UNITY_IOS && UNITY_5_0_0
        UnityEngine.iOS.NotificationServices.RegisterForNotifications(
            NotificationType.Alert |
            NotificationType.Badge |
            NotificationType.Sound);
        #endif

        // track a transaction of 42 US cents
        Mixpanel.People.TrackCharge(0.42);

        // track an event
        Mixpanel.Track("Hello From Unity");

        var args = new Value();
        args["level"] = 84;
        args["coins"] = 99;
        args["health"] = 83.2f;
        //args["bar"]["nested"]["value"] = 20.0; // you can easily create nested objects
        args["unicode"] = "€öäüß✓✓✓✓"; // you can also use unicode strings

        Mixpanel.Track("event with parameters", args);

        Mixpanel.People.Set("gender", "male");

        Mixpanel.StartTimedEvent("time_it");
        // do some lengthy task here
        Mixpanel.Track("time_it");

        // there are also shorthand functions for the special mixpanel properties:
        Mixpanel.People.Name = "Tilo Tester";
        Mixpanel.People.Email = "tilo.tester@example.com";
    }
}