using FMODUnity;
using System;

/// <summary>
/// Key part of the project script architecture
/// It enables decoupled invoking and listening, for any event currently needed
/// </summary>

public abstract class Event { }

// To subscribe: EventBus<EventClass>.OnEvent += Function;
// To invoke event: EventBus<EventClass>.Publish(new EventClass())
public class EventBus<T> where T : Event
{
    public static event Action<T> OnEvent;

    public static void Publish(T pEvent)
    {
        OnEvent?.Invoke(pEvent);
    }
}

#region Controls

public class OnChangePalmMenuActive : Event
{
    // Trigger and set, to control whether palm menu should be visisble
    public OnChangePalmMenuActive(bool isActive) 
    { 
        this.isActive = isActive;
    }
    public bool isActive;
}

public class OnPalmMenuVisibilityChanged : Event
{
    // Notifies of current visibility state of the palm menu
    public OnPalmMenuVisibilityChanged(bool isVisible)
    {
        this.isVisible = isVisible;
    }
    public bool isVisible;
}

public class OnMoved : Event
{
    // Notifies when player has moved (used for input tutorial)
    public OnMoved() { }
}

public class OnTurned : Event
{
    // Notifies when player has turned (used for input tutorial)
    public OnTurned() { }
}

#endregion

#region JulietteAnimations

// Juliette Animation. Look at names, they describe their purpose well ;)
public class OnOpenDoorAnim : Event
{
    public OnOpenDoorAnim() { }
}

public class OnWalkAnim : Event
{
    public OnWalkAnim() { }
}

public class OnSitAnim : Event
{
    public OnSitAnim() { }
}

public class OnIdleAnim : Event
{
    public OnIdleAnim() { }
}

public class OnJulietteAnimFinished : Event
{
    // Important to know which animation finishes, to be able to queue them properly
    public OnJulietteAnimFinished(JulietteAnimations animationType) 
    {
        this.animationType = animationType;
    }

    public JulietteAnimations animationType;
}

#endregion

#region JulietteTalk

public class OnRequestTalk : Event
{
    // Used for triggering dialogue using gestures (free hands)
    public OnRequestTalk() { }
}

public class OnJulietteTalk : Event
{
    // Used to make Juliette talk
    // This ensures the audio is coming from the right direction
    public OnJulietteTalk(EventReference phrase)
    {
        this.phrase = phrase;
    }
    public EventReference phrase;
}

public class OnJulietteFinishedTalk : Event
{
    // Triggered when Juliette stops talking
    public OnJulietteFinishedTalk() { }
}

#endregion

#region Tasks

public class OnStartSimulation : Event
{
    // When the Start button is pressed on the main menu
    public OnStartSimulation() { }
}

public class OnUpdateTask : Event
{
    // Used to update the text on the handheld menu
    public OnUpdateTask() { }
}

public class OnPhoneSendPressed : Event
{
    // Triggered when send button is pressed, in the play scene
    public OnPhoneSendPressed() { }
}

public class OnEnterBuilding : Event
{
    // Triggered when player enters the building
    public OnEnterBuilding() { }
}

public class OnJulietteSitDown : Event
{
    // Triggered after Juliette sat down
    public OnJulietteSitDown() { }
}

public class OnSugarPlacedDown : Event
{
    // Triggered when sugar is placed infront of Juliette
    public OnSugarPlacedDown() { }
}

public class OnPlayerSitDown : Event
{
    // Triggered when player sits down (after sugar has been brought)
    public OnPlayerSitDown(bool isSitting) 
    {
        this.isSitting = isSitting;
    }

    public bool isSitting;
}

#endregion

#region UI

public class OnTransitionScreen : Event
{
    // Triggered to transition screen to black or back to transparent
    public OnTransitionScreen(float targetPercent, float duration)
    {
        this.targetPercent = targetPercent;
        this.duration = duration;
    }
    public float targetPercent;
    public float duration;
}

public class OnShowIndicator : Event
{
    // Trigger and set, to control whether palm menu should be visisble
    public OnShowIndicator(bool isActive, IndicatorHUDs indicator = IndicatorHUDs.EnterHome)
    {
        this.isActive = isActive;
        this.indicator = indicator;
    }
    public bool isActive;
    public IndicatorHUDs indicator;
}

public class OnShowTutorial : Event
{
    // Trigger and set, to control whether palm menu should be visisble
    public OnShowTutorial(bool isActive, TutorialType tutorial = TutorialType.None)
    {
        this.isActive = isActive;
        this.tutorial = tutorial;
    }
    public bool isActive;
    public TutorialType tutorial;
}

public class OnUpdateTalkTimer : Event
{
    // To update progress bar of microphone capture
    public OnUpdateTalkTimer(float currentProgress)
    {
        this.currentProgress = currentProgress;
    }
    public float currentProgress;
}

public class OnShowMicrophonePickup : Event
{
    // Show the microphone UI animation
    public OnShowMicrophonePickup() { }
}

public class OnShowProcessing : Event
{
    // Show the clock processing
    public OnShowProcessing() { }
}

public class OnShowAfterDiscard : Event
{
    // Reset, in case recording gets discarded
    public OnShowAfterDiscard() { }
}

public class OnHideTalk : Event
{
    // Hide everything, regarding Juliette Talk UI
    public OnHideTalk() { }
}

#endregion