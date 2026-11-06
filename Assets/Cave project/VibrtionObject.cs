using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SG; //All SenseGlove Unity Plugin scripts are embedded in the SG namespace.

public class VibratingObject : MonoBehaviour
{
    /// <summary> The Interactable object that we will be sending vibration commands to. </summary>
    /// <remarks> Since SG_Grabable derives from SG_Interactable, this will work for grabables, as well as any other script that derives from SG_Interactable. </remarks>
    [SerializeField]
    private SG_Interactable objectToVibrate;
    [SerializeField]
    private SG_CustomWaveform hapticsWaveform;
    [SerializeField]
    private SG_CustomWaveform stopVibrationWaveform;
    [SerializeField]
    private SG.VibrationLocation vibrationLocation = SG.VibrationLocation.WholeHand;

    //assign this to the on grabbed event of the grabable
    //This will loop until explicitly told to stop
    public void StartVibration()
    {
        objectToVibrate.SendCustomWaveform(hapticsWaveform, vibrationLocation);
    }

    //assign this to the on dropped event of the grabable
    public void StopVibration()
    {
        objectToVibrate.SendCustomWaveform(stopVibrationWaveform, vibrationLocation);
    }
}