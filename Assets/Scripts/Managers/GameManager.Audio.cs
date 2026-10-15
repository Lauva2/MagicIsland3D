using MagicIsland.Configuration;

namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public AudioManager Audio { get; private set; }
        private void UpdateAudioState(GameFlowState state)
        {
            if (Audio == null) return;
            if(state!=GameFlowState.Playing)Audio.SuspendEnvironment();
            Audio.RefreshListeners();
            if (state == GameFlowState.MainMenu) Audio.SetMusic(AudioCue.MenuMusic);
            else if (state == GameFlowState.Playing) Audio.SetMusic(Housing.Inside ? AudioCue.HomeMusic : AudioCue.IslandMusic);
            else if (state == GameFlowState.LoadingScene || state == GameFlowState.InitializingGame || state == GameFlowState.Failed)
            { Audio.StopEffects(); Audio.SetMusic(null); }
            else if (state == GameFlowState.Transitioning || state == GameFlowState.ReturningToMenu) Audio.StopEffects();
        }
    }
}
