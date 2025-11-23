using NAudio.Midi;

namespace MIDI.Core.Midi.Handlers
{
    public static class ControlChangeHandler
    {
        public static void ApplyControlChange(ControlEvent controlEvent, ChannelState state)
        {
            switch (controlEvent.Controller)
            {
                case (int)MidiController.BankSelect:
                    state.BankSelect = controlEvent.Value;
                    break;
                case (int)MidiController.Modulation:
                    state.ModulationMsb = controlEvent.Value;
                    break;
                case (int)MidiController.BreathController:
                    state.BreathControllerMsb = controlEvent.Value;
                    break;
                case (int)MidiController.FootController:
                    state.FootControllerMsb = controlEvent.Value;
                    break;
                case 5:
                    state.PortamentoTimeMsb = controlEvent.Value;
                    break;
                case 6:
                    state.DataEntryMsb = controlEvent.Value;
                    break;
                case (int)MidiController.MainVolume:
                    state.VolumeMsb = controlEvent.Value;
                    break;
                case 8:
                    state.BalanceMsb = controlEvent.Value;
                    break;
                case (int)MidiController.Pan:
                    state.PanMsb = controlEvent.Value;
                    break;
                case (int)MidiController.Expression:
                    state.ExpressionMsb = controlEvent.Value;
                    break;
                case 12:
                    state.EffectControl1 = controlEvent.Value / 127.0f;
                    break;
                case 13:
                    state.EffectControl2 = controlEvent.Value / 127.0f;
                    break;
                case 16:
                    state.GeneralPurposeController1 = controlEvent.Value;
                    break;
                case 17:
                    state.GeneralPurposeController2 = controlEvent.Value;
                    break;
                case 18:
                    state.GeneralPurposeController3 = controlEvent.Value;
                    break;
                case 19:
                    state.GeneralPurposeController4 = controlEvent.Value;
                    break;
                case (int)MidiController.BankSelectLsb:
                    state.BankSelectLsb = controlEvent.Value;
                    break;
                case 33:
                    state.ModulationLsb = controlEvent.Value;
                    break;
                case 34:
                    state.BreathControllerLsb = controlEvent.Value;
                    break;
                case 36:
                    state.FootControllerLsb = controlEvent.Value;
                    break;
                case 37:
                    state.PortamentoTimeLsb = controlEvent.Value;
                    break;
                case 38:
                    state.DataEntryLsb = controlEvent.Value;
                    break;
                case 39:
                    state.VolumeLsb = controlEvent.Value;
                    break;
                case 40:
                    state.BalanceLsb = controlEvent.Value;
                    break;
                case 42:
                    state.PanLsb = controlEvent.Value;
                    break;
                case 43:
                    state.ExpressionLsb = controlEvent.Value;
                    break;
                case 44:
                    state.EffectControl1Lsb = controlEvent.Value / 127.0f;
                    break;
                case 45:
                    state.EffectControl2Lsb = controlEvent.Value / 127.0f;
                    break;
                case (int)MidiController.Sustain:
                    state.Sustain = controlEvent.Value >= 64;
                    break;
                case (int)MidiController.Portamento:
                    state.Portamento = controlEvent.Value >= 64;
                    break;
                case (int)MidiController.Sostenuto:
                    state.Sostenuto = controlEvent.Value >= 64;
                    break;
                case (int)MidiController.SoftPedal:
                    state.SoftPedal = controlEvent.Value >= 64;
                    break;
                case (int)MidiController.LegatoFootswitch:
                    state.LegatoFootswitch = controlEvent.Value >= 64;
                    break;
                case 69:
                    state.Hold2 = controlEvent.Value >= 64;
                    break;
                case 70:
                    state.SoundController1 = controlEvent.Value / 127.0f;
                    break;
                case 80:
                    state.GeneralPurposeController5 = controlEvent.Value;
                    break;
                case 81:
                    state.GeneralPurposeController6 = controlEvent.Value;
                    break;
                case 82:
                    state.GeneralPurposeController7 = controlEvent.Value;
                    break;
                case 83:
                    state.GeneralPurposeController8 = controlEvent.Value;
                    break;
                case 84:
                    state.PortamentoControl = controlEvent.Value;
                    break;
                case 91:
                    state.Effects1Depth = controlEvent.Value / 127.0f;
                    break;
                case 92:
                    state.Effects2Depth = controlEvent.Value / 127.0f;
                    break;
                case 93:
                    state.Effects3Depth = controlEvent.Value / 127.0f;
                    break;
                case 94:
                    state.Effects4Depth = controlEvent.Value / 127.0f;
                    break;
                case 95:
                    state.Effects5Depth = controlEvent.Value / 127.0f;
                    break;
                case 96:
                    state.DataEntry++;
                    break;
                case 97:
                    state.DataEntry--;
                    break;
                case 98:
                    state.NonRegisteredParameterNumberLsb = controlEvent.Value;
                    break;
                case 99:
                    state.NonRegisteredParameterNumberMsb = controlEvent.Value;
                    break;
                case 100:
                    state.RegisteredParameterNumberLsb = controlEvent.Value;
                    break;
                case 101:
                    state.RegisteredParameterNumberMsb = controlEvent.Value;
                    break;
                case 120:
                    break;
                case (int)MidiController.ResetAllControllers:
                    break;
                case 122:
                    break;
                case (int)MidiController.AllNotesOff:
                    break;
                case 124:
                    break;
                case 125:
                    break;
                case 126:
                    break;
                case 127:
                    break;
                default:
                    ApplySoundControllerMappings(controlEvent, state);
                    break;
            }
        }

        private static void ApplySoundControllerMappings(ControlEvent controlEvent, ChannelState state)
        {
            switch (controlEvent.Controller)
            {
                case 71:
                    state.FilterResonanceMultiplier = controlEvent.Value / 127.0;
                    break;
                case 72:
                    state.ReleaseMultiplier = controlEvent.Value / 127.0;
                    break;
                case 73:
                    state.AttackMultiplier = controlEvent.Value / 127.0;
                    break;
                case 74:
                    state.FilterCutoffMultiplier = controlEvent.Value / 127.0;
                    break;
                case 75:
                    state.DecayMultiplier = controlEvent.Value / 127.0;
                    break;
                case 76:
                    state.SoundController7 = controlEvent.Value / 127.0f;
                    break;
                case 77:
                    state.SoundController8 = controlEvent.Value / 127.0f;
                    break;
                case 78:
                    state.SoundController9 = controlEvent.Value / 127.0f;
                    break;
                case 79:
                    state.SoundController10 = controlEvent.Value / 127.0f;
                    break;
            }
        }
    }
}