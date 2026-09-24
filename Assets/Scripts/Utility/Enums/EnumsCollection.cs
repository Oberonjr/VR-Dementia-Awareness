
/// <summary>
/// Collection of different enums
/// </summary>

public enum Mood { Neutral, Happy, Sad, Nostalgic, Furious, Anxious }

public enum TutorialType { Turning, Moving, MenuOpen, Grab, None }
public enum IndicatorHUDs { PressSend, EnterHome, SugarPickup }
public enum JulietteAnimations { OpenDoor, Walk, Sit, IdleStand }

public enum CurrentLanguage{English, Dutch}
// Used for the StoryBeat system to determine if the story should move to a specific other beat or continue in the same context
// NOTE: IF THESE GET CHANGED, OR THEIR VALUES GET CHANGED, PLEASE UPDATE CoreRules.cs AND ANY ASSOCIATED SCRIPTABLE OBJECTS!
public enum StoryProgressResult{Success = 0, Failure = 1, Continue = 2}
// Was replaced by locomotion events, but perhaps might be useful still
public enum JoystickDirection { Any, Up, Horizontal }