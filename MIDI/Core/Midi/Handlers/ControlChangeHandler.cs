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
                case 5: // ポルタメントタイム (PortamentoTime)
                    state.PortamentoTimeMsb = controlEvent.Value;
                    break;
                case 6: // データエントリー (DataEntry)
                    state.DataEntryMsb = controlEvent.Value;
                    break;
                case (int)MidiController.MainVolume:
                    state.VolumeMsb = controlEvent.Value;
                    break;
                case 8: // バランス (Balance)
                    state.BalanceMsb = controlEvent.Value;
                    break;
                case (int)MidiController.Pan:
                    state.PanMsb = controlEvent.Value;
                    break;
                case (int)MidiController.Expression:
                    state.ExpressionMsb = controlEvent.Value;
                    break;
                case 12: // エフェクトコントロール1 (EffectControl1)
                    state.EffectControl1 = controlEvent.Value / 127.0f;
                    break;
                case 13: // エフェクトコントロール2 (EffectControl2)
                    state.EffectControl2 = controlEvent.Value / 127.0f;
                    break;
                case 16: // 汎用コントローラー1 (GeneralPurposeController1)
                    state.GeneralPurposeController1 = controlEvent.Value;
                    break;
                case 17: // 汎用コントローラー2 (GeneralPurposeController2)
                    state.GeneralPurposeController2 = controlEvent.Value;
                    break;
                case 18: // 汎用コントローラー3 (GeneralPurposeController3)
                    state.GeneralPurposeController3 = controlEvent.Value;
                    break;
                case 19: // 汎用コントローラー4 (GeneralPurposeController4)
                    state.GeneralPurposeController4 = controlEvent.Value;
                    break;
                case (int)MidiController.BankSelectLsb:
                    state.BankSelectLsb = controlEvent.Value;
                    break;
                case 33: // モジュレーションLSB (ModulationLsb)
                    state.ModulationLsb = controlEvent.Value;
                    break;
                case 34: // ブレスコントローラーLSB (BreathControllerLsb)
                    state.BreathControllerLsb = controlEvent.Value;
                    break;
                case 36: // フットコントローラーLSB (FootControllerLsb)
                    state.FootControllerLsb = controlEvent.Value;
                    break;
                case 37: // ポルタメントタイムLSB (PortamentoTimeLsb)
                    state.PortamentoTimeLsb = controlEvent.Value;
                    break;
                case 38: // データエントリーLSB (DataEntryLsb)
                    state.DataEntryLsb = controlEvent.Value;
                    break;
                case 39: // メインボリュームLSB (MainVolumeLsb)
                    state.VolumeLsb = controlEvent.Value;
                    break;
                case 40: // バランスLSB (BalanceLsb)
                    state.BalanceLsb = controlEvent.Value;
                    break;
                case 42: // パンLSB (PanLsb)
                    state.PanLsb = controlEvent.Value;
                    break;
                case 43: // エクスプレッションLSB (ExpressionLsb)
                    state.ExpressionLsb = controlEvent.Value;
                    break;
                case 44: // エフェクトコントロール1 LSB (EffectControl1Lsb)
                    state.EffectControl1Lsb = controlEvent.Value / 127.0f;
                    break;
                case 45: // エフェクトコントロール2 LSB (EffectControl2Lsb)
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
                case 69: // ホールド2 (Hold2)
                    state.Hold2 = controlEvent.Value >= 64;
                    break;
                case 70: // サウンドコントローラー1 (Sound Variation)
                    state.SoundController1 = controlEvent.Value / 127.0f;
                    break;
                case 80: // 汎用コントローラー5 (GeneralPurposeController5)
                    state.GeneralPurposeController5 = controlEvent.Value;
                    break;
                case 81: // 汎用コントローラー6 (GeneralPurposeController6)
                    state.GeneralPurposeController6 = controlEvent.Value;
                    break;
                case 82: // 汎用コントローラー7 (GeneralPurposeController7)
                    state.GeneralPurposeController7 = controlEvent.Value;
                    break;
                case 83: // 汎用コントローラー8 (GeneralPurposeController8)
                    state.GeneralPurposeController8 = controlEvent.Value;
                    break;
                case 84: // ポルタメントコントロール (PortamentoControl)
                    state.PortamentoControl = controlEvent.Value;
                    break;
                case 91: // エフェクト1デプス (Reverb) (Effects1Depth)
                    state.Effects1Depth = controlEvent.Value / 127.0f;
                    break;
                case 92: // エフェクト2デプス (Tremolo) (Effects2Depth)
                    state.Effects2Depth = controlEvent.Value / 127.0f;
                    break;
                case 93: // エフェクト3デプス (Chorus) (Effects3Depth)
                    state.Effects3Depth = controlEvent.Value / 127.0f;
                    break;
                case 94: // エフェクト4デプス (Celeste) (Effects4Depth)
                    state.Effects4Depth = controlEvent.Value / 127.0f;
                    break;
                case 95: // エフェクト5デプス (Phaser) (Effects5Depth)
                    state.Effects5Depth = controlEvent.Value / 127.0f;
                    break;
                case 96: // データインクリメント (DataIncrement)
                    state.DataEntry++;
                    break;
                case 97: // データデクリメント (DataDecrement)
                    state.DataEntry--;
                    break;
                case 98: // 非登録パラメーターナンバーLSB (NonRegisteredParameterNumberLsb)
                    state.NonRegisteredParameterNumberLsb = controlEvent.Value;
                    break;
                case 99: // 非登録パラメーターナンバーMSB (NonRegisteredParameterNumberMsb)
                    state.NonRegisteredParameterNumberMsb = controlEvent.Value;
                    break;
                case 100: // 登録パラメーターナンバーLSB (RegisteredParameterNumberLsb)
                    state.RegisteredParameterNumberLsb = controlEvent.Value;
                    break;
                case 101: // 登録パラメーターナンバーMSB (RegisteredParameterNumberMsb)
                    state.RegisteredParameterNumberMsb = controlEvent.Value;
                    break;
                case 120: // オールサウンドオフ (AllSoundOff)
                    break;
                case (int)MidiController.ResetAllControllers:
                    break;
                case 122: // ローカルコントロール (LocalControl)
                    break;
                case (int)MidiController.AllNotesOff:
                    break;
                case 124: // オムニオフ (OmniOff)
                    break;
                case 125: // オムニオン (OmniOn)
                    break;
                case 126: // モノモードオン (MonoModeOn)
                    break;
                case 127: // ポリモードオン (PolyModeOn)
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
                case 71: // サウンドコントローラー2 (Timbre/Resonance)
                    state.FilterResonanceMultiplier = controlEvent.Value / 64.0;
                    break;
                case 72: // サウンドコントローラー3 (Release Time)
                    state.ReleaseMultiplier = controlEvent.Value / 64.0;
                    break;
                case 73: // サウンドコントローラー4 (Attack Time)
                    state.AttackMultiplier = controlEvent.Value / 64.0;
                    break;
                case 74: // サウンドコントローラー5 (Brightness/Cutoff)
                    state.FilterCutoffMultiplier = controlEvent.Value / 64.0;
                    break;
                case 75: // サウンドコントローラー6 (Decay Time)
                    state.DecayMultiplier = controlEvent.Value / 64.0;
                    break;
                case 76: // サウンドコントローラー7 (Vibrato Rate)
                    state.SoundController7 = controlEvent.Value / 127.0f;
                    break;
                case 77: // サウンドコントローラー8 (Vibrato Depth)
                    state.SoundController8 = controlEvent.Value / 127.0f;
                    break;
                case 78: // サウンドコントローラー9 (Vibrato Delay)
                    state.SoundController9 = controlEvent.Value / 127.0f;
                    break;
                case 79: // サウンドコントローラー10 (General Purpose)
                    state.SoundController10 = controlEvent.Value / 127.0f;
                    break;
            }
        }
    }
}