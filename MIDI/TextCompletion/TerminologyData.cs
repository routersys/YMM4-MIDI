using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MIDI.TextCompletion.Models;

namespace MIDI.TextCompletion
{
    public static class TerminologyData
    {
        public static ObservableCollection<MusicTerm> GetDefaultTerms()
        {
            return new ObservableCollection<MusicTerm>
            {
                new MusicTerm {
                    JapaneseName = "MIDI", EnglishName = "Musical Instrument Digital Interface",
                    Description = "電子楽器同士やコンピューターなどを接続し、演奏情報をデジタル信号として送受信するための世界共通規格。",
                    Aliases = { "ミディ" }
                },
                new MusicTerm {
                    JapaneseName = "ベロシティ", EnglishName = "Velocity",
                    Description = "MIDIノートオンメッセージに含まれる、鍵盤を押す速さ（強さ）を表す値。通常0から127の範囲で音量や音色を制御する。",
                    Aliases = { "Velocity" }
                },
                new MusicTerm {
                    JapaneseName = "クオンタイズ", EnglishName = "Quantize",
                    Description = "MIDIノートのタイミングを指定した音価（例：16分音符）のグリッドに合わせて補正する機能。",
                    Aliases = { "Quantize", "タイミング補正" }
                },
                new MusicTerm {
                    JapaneseName = "ピッチベンド", EnglishName = "Pitch Bend",
                    Description = "MIDIメッセージの一つで、音の高さを滑らかに変化させるための制御情報。",
                    Aliases = { "PitchBend" }
                },
                new MusicTerm {
                    JapaneseName = "DAW", EnglishName = "Digital Audio Workstation",
                    Description = "コンピューター上で音楽制作を行うためのソフトウェアシステム全般。",
                    Aliases = { "ダウ", "daw" }
                },
                new MusicTerm {
                    JapaneseName = "VST", EnglishName = "Virtual Studio Technology",
                    Description = "Steinberg社が開発した、ソフトウェア音源やエフェクトプラグインの規格。",
                    Aliases = { "vst", "ブイエスティー" }
                },
                new MusicTerm {
                    JapaneseName = "サンプリングレート", EnglishName = "Sampling Rate",
                    Description = "アナログ音声をデジタル化する際に、1秒あたりに標本化（サンプリング）する回数。Hzで表される。",
                    Aliases = { "SamplingRate", "サンプリング周波数" }
                },
                new MusicTerm {
                    JapaneseName = "ビット深度", EnglishName = "Bit Depth",
                    Description = "デジタルオーディオにおいて、各サンプルの振幅情報を記録するために使用するビット数。大きいほどダイナミックレンジが広がる。",
                    Aliases = { "BitDepth", "量子化ビット数" }
                },
                new MusicTerm {
                    JapaneseName = "BPM", EnglishName = "Beats Per Minute",
                    Description = "音楽のテンポ（速度）を表す単位。1分あたりの拍数。",
                    Aliases = { "bpm", "テンポ" }
                },
                new MusicTerm {
                    JapaneseName = "アルペジオ", EnglishName = "Arpeggio",
                    Description = "和音の構成音を同時に鳴らすのではなく、順番に弾く演奏技法。",
                    Aliases = { "Arpeggio" }
                },
                new MusicTerm {
                    JapaneseName = "コントロールチェンジ", EnglishName = "Control Change",
                    Description = "音色、音量、パンニングなど、音の様々な側面を制御するためのMIDIメッセージ。",
                    Aliases = { "CC", "ControlChange" }
                },
                new MusicTerm {
                    JapaneseName = "プログラムチェンジ", EnglishName = "Program Change",
                    Description = "MIDI音源の音色（プログラム）を切り替えるためのMIDIメッセージ。",
                    Aliases = { "PC", "ProgramChange" }
                },
                new MusicTerm {
                    JapaneseName = "アフタータッチ", EnglishName = "Aftertouch",
                    Description = "鍵盤を押し込んだ後、さらに圧力を加えることで音色に変化をつけるMIDIデータ。",
                    Aliases = { "Aftertouch" }
                },
                new MusicTerm {
                    JapaneseName = "シーケンサー", EnglishName = "Sequencer",
                    Description = "MIDIの演奏情報を録音、編集、再生するための機器またはソフトウェア。",
                    Aliases = { "Sequencer" }
                },
                new MusicTerm {
                    JapaneseName = "シンセサイザー", EnglishName = "Synthesizer",
                    Description = "音を電子的に合成（シンセサイズ）する楽器。",
                    Aliases = { "Synth", "シンセ" }
                },
                new MusicTerm {
                    JapaneseName = "サンプラー", EnglishName = "Sampler",
                    Description = "録音した音（サンプル）を加工し、楽器として演奏できる機器またはソフトウェア。",
                    Aliases = { "Sampler" }
                },
                new MusicTerm {
                    JapaneseName = "モジュレーション", EnglishName = "Modulation",
                    Description = "音の高さや音量、音色などを周期的に変化させること。通常はCC1で制御される。",
                    Aliases = { "Modulation", "ビブラート" }
                },
                new MusicTerm {
                    JapaneseName = "リバーブ", EnglishName = "Reverb",
                    Description = "音に残響音（響き）を加えるエフェクト。",
                    Aliases = { "Reverb", "残響" }
                },
                new MusicTerm {
                    JapaneseName = "ディレイ", EnglishName = "Delay",
                    Description = "音を遅延させて、やまびこのような効果を生み出すエフェクト。",
                    Aliases = { "Delay", "やまびこ" }
                },
                new MusicTerm {
                    JapaneseName = "コンプレッサー", EnglishName = "Compressor",
                    Description = "音量の大きい部分を圧縮し、音量のばらつきを抑えるエフェクト。",
                    Aliases = { "Comp", "コンプ" }
                },
                new MusicTerm {
                    JapaneseName = "イコライザー", EnglishName = "Equalizer",
                    Description = "特定の周波数帯域を強調したり、減衰させたりして音色を調整するエフェクト。",
                    Aliases = { "EQ", "イーキュー" }
                },
                new MusicTerm {
                    JapaneseName = "パンニング", EnglishName = "Panning",
                    Description = "ステレオ音場における音の左右の定位（位置）を調整すること。",
                    Aliases = { "Pan", "パン" }
                },
                new MusicTerm {
                    JapaneseName = "オートメーション", EnglishName = "Automation",
                    Description = "音量やパン、エフェクトのパラメータなどを時間と共に自動的に変化させる機能。",
                    Aliases = { "Automation" }
                },
                new MusicTerm {
                    JapaneseName = "小節", EnglishName = "Measure / Bar",
                    Description = "音楽の一定の拍のまとまり。拍子によって区切られる。",
                    Aliases = { "Bar", "Measure" }
                },
                new MusicTerm {
                    JapaneseName = "ノートオン", EnglishName = "Note On",
                    Description = "MIDIノートの発音を開始するメッセージ。",
                    Aliases = { "NoteOn" }
                },
                new MusicTerm {
                    JapaneseName = "ノートオフ", EnglishName = "Note Off",
                    Description = "MIDIノートの発音を停止するメッセージ。",
                    Aliases = { "NoteOff" }
                },
                new MusicTerm {
                    JapaneseName = "システムエクスクルーシブ", EnglishName = "System Exclusive",
                    Description = "特定の機器メーカー専用の機能（音色パラメータなど）を設定するためのMIDIメッセージ。",
                    Aliases = { "SysEx" }
                },
                new MusicTerm {
                    JapaneseName = "サウンドフォント", EnglishName = "SoundFont",
                    Description = "サンプリングされた音源をMIDI音源として利用するためのファイル形式。",
                    Aliases = { "sf2" }
                },
                new MusicTerm {
                    JapaneseName = "レイテンシー", EnglishName = "Latency",
                    Description = "信号が入力されてから出力されるまでの遅延時間。",
                    Aliases = { "Latency", "遅延" }
                },
                new MusicTerm {
                    JapaneseName = "オーディオインターフェース", EnglishName = "Audio Interface",
                    Description = "コンピューターとマイクやスピーカーなどのオーディオ機器を接続するための機器。",
                    Aliases = { "AudioInterface" }
                },
                new MusicTerm {
                    JapaneseName = "フランジャー", EnglishName = "Flanger",
                    Description = "音をわずかに遅延させて原音とミックスし、うねるような効果を生むエフェクト。",
                    Aliases = { "Flanger" }
                },
                new MusicTerm {
                    JapaneseName = "フェイザー", EnglishName = "Phaser",
                    Description = "位相をずらした音を原音とミックスし、シュワシュワとした独特のうねりを生むエフェクト。",
                    Aliases = { "Phaser" }
                },
                new MusicTerm {
                    JapaneseName = "コーラス", EnglishName = "Chorus",
                    Description = "音をわずかに遅延させ、ピッチを揺らして原音とミックスし、音に厚みや広がりを持たせるエフェクト。",
                    Aliases = { "Chorus" }
                },
                new MusicTerm {
                    JapaneseName = "トレモロ", EnglishName = "Tremolo",
                    Description = "音量を周期的に変化させるエフェクト。",
                    Aliases = { "Tremolo" }
                },
                new MusicTerm {
                    JapaneseName = "ビブラート", EnglishName = "Vibrato",
                    Description = "音高を周期的に変化させる（揺らす）エフェクトまたは演奏技法。",
                    Aliases = { "Vibrato" }
                },
                new MusicTerm {
                    JapaneseName = "オートパン", EnglishName = "Auto Pan",
                    Description = "ステレオの左右の定位（パン）を自動的に周期的に動かすエフェクト。",
                    Aliases = { "AutoPan" }
                },
                new MusicTerm {
                    JapaneseName = "リングモジュレーター", EnglishName = "Ring Modulator",
                    Description = "二つの信号を乗算し、金属的・鐘のような複雑な倍音を生み出すエフェクト。",
                    Aliases = { "RingModulator" }
                },
                new MusicTerm {
                    JapaneseName = "ビットクラッシャー", EnglishName = "Bitcrusher",
                    Description = "サンプリングレートやビット深度を意図的に下げることで、ローファイでザラザラした音にするエフェクト。",
                    Aliases = { "Bitcrusher", "ローファイ" }
                },
                new MusicTerm {
                    JapaneseName = "ディストーション", EnglishName = "Distortion",
                    Description = "音を強く歪ませて、倍音を付加するエフェクト。オーバードライブより一般的に歪みが深い。",
                    Aliases = { "Distortion", "歪み" }
                },
                new MusicTerm {
                    JapaneseName = "オーバードライブ", EnglishName = "Overdrive",
                    Description = "真空管アンプを過大入力した時のような、穏やかで温かみのある歪みを生むエフェクト。",
                    Aliases = { "Overdrive" }
                },
                new MusicTerm {
                    JapaneseName = "ファズ", EnglishName = "Fuzz",
                    Description = "意図的に信号を矩形波に近くなるまで強く歪ませる、ノイジーなエフェクト。",
                    Aliases = { "Fuzz" }
                },
                new MusicTerm {
                    JapaneseName = "サチュレーション", EnglishName = "Saturation",
                    Description = "アナログテープや真空管を通した時のような、自然な歪みと倍音を付加し、音に暖かみや太さを与える処理。",
                    Aliases = { "Saturation", "サチュレーター" }
                },
                new MusicTerm {
                    JapaneseName = "エンハンサー", EnglishName = "Enhancer",
                    Description = "高次の倍音を強調または生成することで、音の明瞭度やきらびやかさを増すエフェクト。",
                    Aliases = { "Enhancer", "エキサイター" }
                },
                new MusicTerm {
                    JapaneseName = "リミッター", EnglishName = "Limiter",
                    Description = "設定したスレッショルド（閾値）を音が超えないように強力に圧縮するエフェクト。主に音圧稼ぎに使われる。",
                    Aliases = { "Limiter" }
                },
                new MusicTerm {
                    JapaneseName = "マキシマイザー", EnglishName = "Maximizer",
                    Description = "音圧（聴感上の音量）を最大限に高めることを目的としたリミッターの一種。",
                    Aliases = { "Maximizer" }
                },
                new MusicTerm {
                    JapaneseName = "ゲート", EnglishName = "Gate",
                    Description = "設定した音量（スレッショルド）以下の小さな音をカットするエフェクト。ノイズ除去などに使われる。",
                    Aliases = { "NoiseGate", "ノイズゲート" }
                },
                new MusicTerm {
                    JapaneseName = "エクスパンダー", EnglishName = "Expander",
                    Description = "ゲートとは逆に、設定した音量（スレッショルド）を超えた音をより大きくするエフェクト。ダイナミックレンジを広げる。",
                    Aliases = { "Expander" }
                },
                new MusicTerm {
                    JapaneseName = "ディエッサー", EnglishName = "De-Esser",
                    Description = "ボーカルの「サ行」などの歯擦音（シビランス）を抑えるための特殊なコンプレッサー。",
                    Aliases = { "DeEsser" }
                },
                new MusicTerm {
                    JapaneseName = "ダッキング", EnglishName = "Ducking",
                    Description = "特定の音（例：キックドラム）が鳴った瞬間に、別の音（例：ベース）の音量を自動的に下げるテクニック。",
                    Aliases = { "Ducking", "サイドチェイン" }
                },
                new MusicTerm {
                    JapaneseName = "オシレーター", EnglishName = "Oscillator",
                    Description = "シンセサイザーにおいて音の元となる波形を生成する部分。",
                    Aliases = { "OSC" }
                },
                new MusicTerm {
                    JapaneseName = "ノコギリ波", EnglishName = "Sawtooth Wave",
                    Description = "倍音を豊富に含む、明るく鋭い音色が特徴の基本的な波形。",
                    Aliases = { "Saw", "Sawtooth" }
                },
                new MusicTerm {
                    JapaneseName = "矩形波", EnglishName = "Square Wave",
                    Description = "奇数倍音のみを含む、中空的でファミコンのような音色が特徴の基本的な波形。",
                    Aliases = { "Square", "Pulse" }
                },
                new MusicTerm {
                    JapaneseName = "サイン波", EnglishName = "Sine Wave",
                    Description = "倍音を含まない、最も純粋な（基音のみの）波形。",
                    Aliases = { "Sine" }
                },
                new MusicTerm {
                    JapaneseName = "三角波", EnglishName = "Triangle Wave",
                    Description = "矩形波に似ているが、高次の奇数倍音が少ないため、より丸く柔らかい音色の波形。",
                    Aliases = { "Triangle" }
                },
                new MusicTerm {
                    JapaneseName = "ノイズ", EnglishName = "Noise",
                    Description = "特定の周波数を持たない「サー」という音。パーカッションや効果音の作成に使われる。",
                    Aliases = { "Noise" }
                },
                new MusicTerm {
                    JapaneseName = "LFO", EnglishName = "Low Frequency Oscillator",
                    Description = "人間の耳には聞こえないほど低い周波数（通常20Hz以下）で波形を生成し、音量やピッチ、フィルターなどに周期的な変化（揺れ）を与えるための装置。",
                    Aliases = { "lfo" }
                },
                new MusicTerm {
                    JapaneseName = "エンベロープ", EnglishName = "Envelope",
                    Description = "音の時間的な変化（立ち上がり、減衰など）を設定する機能。ADSRが最も一般的。",
                    Aliases = { "EG", "EnvelopeGenerator" }
                },
                new MusicTerm {
                    JapaneseName = "アタック", EnglishName = "Attack",
                    Description = "ADSRの一部。鍵盤が押されてから音が最大音量に達するまでの時間。",
                    Aliases = { "Attack" }
                },
                new MusicTerm {
                    JapaneseName = "ディケイ", EnglishName = "Decay",
                    Description = "ADSRの一部。アタックで最大音量に達した後、サスティンレベルまで減衰する時間。",
                    Aliases = { "Decay" }
                },
                new MusicTerm {
                    JapaneseName = "サスティン", EnglishName = "Sustain",
                    Description = "ADSRの一部。鍵盤が押されている間、保持される音量レベル。",
                    Aliases = { "Sustain" }
                },
                new MusicTerm {
                    JapaneseName = "リリース", EnglishName = "Release",
                    Description = "ADSRの一部。鍵盤が離されてから音が完全に消える（音量が0になる）までの時間。",
                    Aliases = { "Release" }
                },
                new MusicTerm {
                    JapaneseName = "フィルター", EnglishName = "Filter",
                    Description = "シンセサイザーにおいて、オシレーターが生成した音から特定の周波数帯域を削ったり強調したりして音色を加工する機能。",
                    Aliases = { "Filter" }
                },
                new MusicTerm {
                    JapaneseName = "カットオフ周波数", EnglishName = "Cutoff Frequency",
                    Description = "フィルターが効果を発揮し始める周波数のポイント。",
                    Aliases = { "Cutoff" }
                },
                new MusicTerm {
                    JapaneseName = "レゾナンス", EnglishName = "Resonance",
                    Description = "フィルターのカットオフ周波数付近を強調する機能。上げると「ミョーン」というようなクセのある音になる。",
                    Aliases = { "Resonance", "Q" }
                },
                new MusicTerm {
                    JapaneseName = "ローパスフィルター", EnglishName = "Low Pass Filter",
                    Description = "設定したカットオフ周波数よりも低い周波数帯域のみを通過させる（高い周波数をカットする）フィルター。",
                    Aliases = { "LPF" }
                },
                new MusicTerm {
                    JapaneseName = "ハイパスフィルター", EnglishName = "High Pass Filter",
                    Description = "設定したカットオフ周波数よりも高い周波数帯域のみを通過させる（低い周波数をカットする）フィルター。",
                    Aliases = { "HPF" }
                },
                new MusicTerm {
                    JapaneseName = "バンドパスフィルター", EnglishName = "Band Pass Filter",
                    Description = "設定した特定の周波数帯域のみを通過させるフィルター。",
                    Aliases = { "BPF" }
                },
                new MusicTerm {
                    JapaneseName = "減算合成", EnglishName = "Subtractive Synthesis",
                    Description = "倍音を多く含む波形（ノコギリ波など）をフィルターで削っていくことで音色を作る、最も一般的なシンセサイザーの方式。",
                    Aliases = { "Subtractive" }
                },
                new MusicTerm {
                    JapaneseName = "FM合成", EnglishName = "Frequency Modulation",
                    Description = "あるオシレーター（キャリア）を別のオシレーター（モジュレーター）で変調させることにより、複雑な倍音を生み出す合成方式。",
                    Aliases = { "FM" }
                },
                new MusicTerm {
                    JapaneseName = "ウェーブテーブル合成", EnglishName = "Wavetable Synthesis",
                    Description = "複数の異なる波形をテーブルとして保持し、それらを滑らかに切り替えることで音色を時間的に変化させる合成方式。",
                    Aliases = { "Wavetable" }
                },
                new MusicTerm {
                    JapaneseName = "パッチ", EnglishName = "Patch",
                    Description = "シンセサイザーやエフェクトの設定（音色）を保存したもの。",
                    Aliases = { "Preset", "プリセット", "音色" }
                },
                new MusicTerm {
                    JapaneseName = "モノフォニック", EnglishName = "Monophonic",
                    Description = "同時に一つの音しか発音できないシンセサイザーの動作モード。",
                    Aliases = { "Mono" }
                },
                new MusicTerm {
                    JapaneseName = "ポリフォニック", EnglishName = "Polyphonic",
                    Description = "同時に複数の音（和音）を発音できるシンセサイザーの動作モード。",
                    Aliases = { "Poly" }
                },
                new MusicTerm {
                    JapaneseName = "ポルタメント", EnglishName = "Portamento",
                    Description = "ある音から次の音へ移る際に、ピッチを滑らかに変化させる機能。",
                    Aliases = { "Portamento", "グライド" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIチャンネル", EnglishName = "MIDI Channel",
                    Description = "MIDI信号を送受信する経路を区別するための番号。1から16まである。",
                    Aliases = { "MIDI Ch" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIポート", EnglishName = "MIDI Port",
                    Description = "MIDI機器を物理的または仮想的に接続する入出力端子。",
                    Aliases = { "MIDI Port" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIインターフェース", EnglishName = "MIDI Interface",
                    Description = "コンピューターとMIDI機器（キーボードなど）を接続するための機器。",
                    Aliases = { "MIDI I/F" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIマージ", EnglishName = "MIDI Merge",
                    Description = "複数のMIDI信号を一つにまとめる機能。",
                    Aliases = { "Merge" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIスルー", EnglishName = "MIDI Thru",
                    Description = "MIDI IN端子から入った信号を、そのままMIDI THRU端子から出力する機能。",
                    Aliases = { "Thru" }
                },
                new MusicTerm {
                    JapaneseName = "MTC", EnglishName = "MIDI Time Code",
                    Description = "複数のMIDI機器やDAWを時間で同期させるためのMIDI信号。",
                    Aliases = { "MTC" }
                },
                new MusicTerm {
                    JapaneseName = "MMC", EnglishName = "MIDI Machine Control",
                    Description = "DAWやMTRの再生、停止、早送りなどの操作をMIDIメッセージで行うための規格。",
                    Aliases = { "MMC" }
                },
                new MusicTerm {
                    JapaneseName = "GM", EnglishName = "General MIDI",
                    Description = "MIDI音源の音色配列やドラムマップなどを共通化した規格。GM対応機器ならどのメーカーでも同じような音色で再生できる。",
                    Aliases = { "GeneralMIDI" }
                },
                new MusicTerm {
                    JapaneseName = "GS", EnglishName = "Roland GS",
                    Description = "Roland社がGM規格を独自に拡張した規格。",
                    Aliases = { "GS" }
                },
                new MusicTerm {
                    JapaneseName = "XG", EnglishName = "Yamaha XG",
                    Description = "Yamaha社がGM規格を独自に拡張した規格。",
                    Aliases = { "XG" }
                },
                new MusicTerm {
                    JapaneseName = "SMF", EnglishName = "Standard MIDI File",
                    Description = "MIDIの演奏データを保存するための標準的なファイル形式。",
                    Aliases = { "StandardMIDIFile", ".mid" }
                },
                new MusicTerm {
                    JapaneseName = "トラック", EnglishName = "Track",
                    Description = "DAW上で、楽器ごとやパートごとに演奏情報を記録するレーン。",
                    Aliases = { "Track" }
                },
                new MusicTerm {
                    JapaneseName = "オーディオトラック", EnglishName = "Audio Track",
                    Description = "DAW上で、録音された波形データ（オーディオ）を扱うトラック。",
                    Aliases = { "AudioTrack" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIトラック", EnglishName = "MIDI Track",
                    Description = "DAW上で、MIDIデータを記録・編集するためのトラック。",
                    Aliases = { "MidiTrack" }
                },
                new MusicTerm {
                    JapaneseName = "インストゥルメントトラック", EnglishName = "Instrument Track",
                    Description = "MIDIトラックとソフトウェア音源（VSTiなど）が一体化したDAWのトラック。",
                    Aliases = { "InstrumentTrack" }
                },
                new MusicTerm {
                    JapaneseName = "AUXトラック", EnglishName = "AUX Track",
                    Description = "DAWのミキサー上で、主にエフェクト（リバーブなど）を送るために使われる補助的なトラック。",
                    Aliases = { "AUX" }
                },
                new MusicTerm {
                    JapaneseName = "バストラック", EnglishName = "Bus Track",
                    Description = "DAW上で、複数のトラックの音声を一つにまとめるためのトラック。",
                    Aliases = { "Bus", "GroupTrack" }
                },
                new MusicTerm {
                    JapaneseName = "マスタートラック", EnglishName = "Master Track",
                    Description = "DAWの全ての音声が最終的に集約されるトラック。全体の音量やエフェクトを管理する。",
                    Aliases = { "MasterTrack", "StereoOut" }
                },
                new MusicTerm {
                    JapaneseName = "ミキサー", EnglishName = "Mixer",
                    Description = "DAW上で、各トラックの音量バランス、パンニング、エフェクトなどを調整する画面。",
                    Aliases = { "Mixer" }
                },
                new MusicTerm {
                    JapaneseName = "フェーダー", EnglishName = "Fader",
                    Description = "ミキサー上で、各トラックの音量を調整するためのツマミ。",
                    Aliases = { "Fader" }
                },
                new MusicTerm {
                    JapaneseName = "ソロ", EnglishName = "Solo",
                    Description = "指定したトラックの音だけを再生し、他のトラックを一時的にミュートする機能。",
                    Aliases = { "Solo" }
                },
                new MusicTerm {
                    JapaneseName = "ミュート", EnglishName = "Mute",
                    Description = "指定したトラックの音を一時的に消音する機能。",
                    Aliases = { "Mute" }
                },
                new MusicTerm {
                    JapaneseName = "リージョン", EnglishName = "Region",
                    Description = "DAWのタイムライン上に配置される、オーディオやMIDIデータの断片。",
                    Aliases = { "Region", "クリップ" }
                },
                new MusicTerm {
                    JapaneseName = "タイムライン", EnglishName = "Timeline",
                    Description = "DAW上で、時間軸に沿ってリージョンやクリップを配置するメインの編集領域。",
                    Aliases = { "Timeline", "アレンジウィンドウ" }
                },
                new MusicTerm {
                    JapaneseName = "グリッド", EnglishName = "Grid",
                    Description = "DAWのタイムライン上に表示される、小節や拍を示す縦線。スナップ機能の基準となる。",
                    Aliases = { "Grid" }
                },
                new MusicTerm {
                    JapaneseName = "スナップ", EnglishName = "Snap",
                    Description = "リージョンやMIDIノートの配置・編集を、グリッド（拍や小節）に自動的に合わせる機能。",
                    Aliases = { "Snap" }
                },
                new MusicTerm {
                    JapaneseName = "ループ", EnglishName = "Loop",
                    Description = "指定した区間を繰り返し再生する機能。または、繰り返し演奏するために作られた短いオーディオやMIDIの素材。",
                    Aliases = { "Loop" }
                },
                new MusicTerm {
                    JapaneseName = "パンチイン", EnglishName = "Punch In",
                    Description = "録音中、指定した箇所から自動的に録音を開始する機能。",
                    Aliases = { "PunchIn" }
                },
                new MusicTerm {
                    JapaneseName = "パンチアウト", EnglishName = "Punch Out",
                    Description = "録音中、指定した箇所で自動的に録音を終了する機能。",
                    Aliases = { "PunchOut" }
                },
                new MusicTerm {
                    JapaneseName = "バウンス", EnglishName = "Bounce",
                    Description = "DAWのプロジェクトを再生し、その結果を一つのオーディオファイルとして書き出す（エクスポートする）こと。",
                    Aliases = { "Bounce", "ミックスダウン" }
                },
                new MusicTerm {
                    JapaneseName = "フリーズ", EnglishName = "Freeze",
                    Description = "CPU負荷の高いソフトウェア音源やエフェクトがかかったトラックを、一時的にオーディオファイル化して負荷を軽減する機能。",
                    Aliases = { "Freeze" }
                },
                new MusicTerm {
                    JapaneseName = "テンポトラック", EnglishName = "Tempo Track",
                    Description = "DAW上で、曲の途中でテンポ（BPM）を変更するための設定を行うトラック。",
                    Aliases = { "TempoTrack" }
                },
                new MusicTerm {
                    JapaneseName = "マーカー", EnglishName = "Marker",
                    Description = "DAWのタイムライン上に、曲の構成（Aメロ、サビなど）を示す目印を付ける機能。",
                    Aliases = { "Marker" }
                },
                new MusicTerm {
                    JapaneseName = "トランスポーズ", EnglishName = "Transpose",
                    Description = "MIDIデータやオーディオデータの音高を、まとめて移調（上げ下げ）する機能。",
                    Aliases = { "Transpose", "移調" }
                },
                new MusicTerm {
                    JapaneseName = "ピアノロール", EnglishName = "Piano Roll",
                    Description = "MIDIデータを視覚的に編集するためのエディタ画面。縦軸が音高、横軸が時間を表す。",
                    Aliases = { "PianoRoll" }
                },
                new MusicTerm {
                    JapaneseName = "モノラル", EnglishName = "Monaural",
                    Description = "音声を1チャンネル（1つのスピーカー）で再生する方式。",
                    Aliases = { "Mono" }
                },
                new MusicTerm {
                    JapaneseName = "ステレオ", EnglishName = "Stereo",
                    Description = "音声を左右2チャンネル（2つのスピーカー）で再生し、音の広がりや定位を表現する方式。",
                    Aliases = { "Stereo" }
                },
                new MusicTerm {
                    JapaneseName = "dB", EnglishName = "Decibel",
                    Description = "デシベル。音量や音圧のレベルを表す単位。",
                    Aliases = { "デシベル" }
                },
                new MusicTerm {
                    JapaneseName = "周波数", EnglishName = "Frequency",
                    Description = "音波が1秒間に振動する回数。Hz（ヘルツ）で表され、音の高さ（ピッチ）を決定する。",
                    Aliases = { "Frequency", "ヘルツ" }
                },
                new MusicTerm {
                    JapaneseName = "振幅", EnglishName = "Amplitude",
                    Description = "音波の振動の幅。音の大きさ（音量）を決定する。",
                    Aliases = { "Amplitude" }
                },
                new MusicTerm {
                    JapaneseName = "波形", EnglishName = "Waveform",
                    Description = "音波の振動の様子を視覚的にグラフ化したもの。音色を決定する。",
                    Aliases = { "Waveform" }
                },
                new MusicTerm {
                    JapaneseName = "PCM", EnglishName = "Pulse Code Modulation",
                    Description = "アナログの音声信号をデジタルデータに変換する最も一般的な方式。",
                    Aliases = { "PCM" }
                },
                new MusicTerm {
                    JapaneseName = "WAV", EnglishName = "WAV",
                    Description = "Windowsで標準的に使われる非圧縮のオーディオファイル形式。",
                    Aliases = { "wav" }
                },
                new MusicTerm {
                    JapaneseName = "AIFF", EnglishName = "AIFF",
                    Description = "Macで標準的に使われる非圧縮のオーディオファイル形式。",
                    Aliases = { "aiff" }
                },
                new MusicTerm {
                    JapaneseName = "MP3", EnglishName = "MP3",
                    Description = "音質を保ちながらデータ容量を大幅に圧縮する、最も一般的な音声圧縮形式。",
                    Aliases = { "mp3" }
                },
                new MusicTerm {
                    JapaneseName = "AAC", EnglishName = "Advanced Audio Coding",
                    Description = "MP3よりも高い圧縮率と音質を持つとされる音声圧縮形式。",
                    Aliases = { "aac" }
                },
                new MusicTerm {
                    JapaneseName = "FLAC", EnglishName = "FLAC",
                    Description = "音質を劣化させずにデータ容量を圧縮できる可逆圧縮（ロスレス）形式。",
                    Aliases = { "flac", "ロスレス" }
                },
                new MusicTerm {
                    JapaneseName = "ダイナミックレンジ", EnglishName = "Dynamic Range",
                    Description = "オーディオ機器や楽曲における、最も小さい音と最も大きい音の差（幅）。",
                    Aliases = { "DynamicRange" }
                },
                new MusicTerm {
                    JapaneseName = "S/N比", EnglishName = "Signal-to-Noise Ratio",
                    Description = "信号（Signal）とノイズ（Noise）のレベル差。この値が大きいほどノイズが少なく高音質とされる。",
                    Aliases = { "SN比" }
                },
                new MusicTerm {
                    JapaneseName = "ヘッドルーム", EnglishName = "Headroom",
                    Description = "デジタルオーディオにおいて、音量が最大レベル（0dBFS）に達するまでの余裕（マージン）。",
                    Aliases = { "Headroom" }
                },
                new MusicTerm {
                    JapaneseName = "クリッピング", EnglishName = "Clipping",
                    Description = "デジタルオーディオにおいて、音量が最大レベル（0dBFS）を超えてしまい、波形が潰れてノイズが発生すること。",
                    Aliases = { "Clipping", "音割れ" }
                },
                new MusicTerm {
                    JapaneseName = "ノーマライズ", EnglishName = "Normalize",
                    Description = "オーディオファイルの音量が最大になるように、全体の音量を均一に引き上げる処理。",
                    Aliases = { "Normalize" }
                },
                new MusicTerm {
                    JapaneseName = "ASIO", EnglishName = "Audio Stream Input/Output",
                    Description = "Steinberg社が開発した、Windows環境でオーディオの入出力の遅延（レイテンシー）を極力少なくするための規格（ドライバー）。",
                    Aliases = { "ASIO", "アシオ" }
                },
                new MusicTerm {
                    JapaneseName = "Core Audio", EnglishName = "Core Audio",
                    Description = "macOSに標準搭載されている、低遅延で高機能なオーディオドライバー。",
                    Aliases = { "CoreAudio" }
                },
                new MusicTerm {
                    JapaneseName = "音階", EnglishName = "Scale",
                    Description = "音を高さの順に並べたもの。ドレミファソラシドなど。",
                    Aliases = { "Scale", "スケール" }
                },
                new MusicTerm {
                    JapaneseName = "メジャースケール", EnglishName = "Major Scale",
                    Description = "長音階。明るい響きを持つ最も基本的な音階。（全・全・半・全・全・全・半）",
                Aliases = { "MajorScale", "長音階" }
                },
                new MusicTerm {
                    JapaneseName = "マイナースケール", EnglishName = "Minor Scale",
                    Description = "短音階。暗い、悲しい響きを持つ音階。自然短音階、和声的短音階、旋律的短音階の3種類がある。",
                    Aliases = { "MinorScale", "短音階" }
                },
                new MusicTerm {
                    JapaneseName = "コード", EnglishName = "Chord",
                    Description = "高さの異なる複数の音を同時に鳴らしたときの響き。和音。",
                    Aliases = { "Chord", "和音" }
                },
                new MusicTerm {
                    JapaneseName = "メジャーコード", EnglishName = "Major Chord",
                    Description = "明るい響きを持つ基本的な三和音。根音（ルート）、長3度、完全5度で構成される。",
                    Aliases = { "MajorChord", "長三和音" }
                },
                new MusicTerm {
                    JapaneseName = "マイナーコード", EnglishName = "Minor Chord",
                    Description = "暗い響きを持つ基本的な三和音。根音（ルート）、短3度、完全5度で構成される。",
                    Aliases = { "MinorChord", "短三和音" }
                },
                new MusicTerm {
                    JapaneseName = "セブンスコード", EnglishName = "Seventh Chord",
                    Description = "三和音にさらに第7音（根音から7度上の音）を加えた四和音。",
                    Aliases = { "SeventhChord", "七の和音" }
                },
                new MusicTerm {
                    JapaneseName = "テンションノート", EnglishName = "Tension Note",
                    Description = "コードの響きに緊張感（テンション）を加えるために、基本的なコード構成音（1, 3, 5, 7）に付加される音（9, 11, 13など）。",
                    Aliases = { "Tension" }
                },
                new MusicTerm {
                    JapaneseName = "転回形", EnglishName = "Inversion",
                    Description = "和音の構成音のうち、根音（ルート）以外の音が最低音になる形。",
                    Aliases = { "Inversion" }
                },
                new MusicTerm {
                    JapaneseName = "拍子", EnglishName = "Time Signature",
                    Description = "曲のリズムの基本的な単位。1小節に何拍あるかを示す（例：4/4拍子、3/4拍子）。",
                    Aliases = { "TimeSignature" }
                },
                new MusicTerm {
                    JapaneseName = "調", EnglishName = "Key",
                    Description = "楽曲の中心となる音（主音）と音階（長調または短調）のこと。ハ長調、イ短調など。",
                    Aliases = { "Key" }
                },
                new MusicTerm {
                    JapaneseName = "シャープ", EnglishName = "Sharp",
                    Description = "音を半音高くすることを示す記号（♯）。",
                    Aliases = { "♯" }
                },
                new MusicTerm {
                    JapaneseName = "フラット", EnglishName = "Flat",
                    Description = "音を半音低くすることを示す記号（♭）。",
                    Aliases = { "♭" }
                },
                new MusicTerm {
                    JapaneseName = "ナチュラル", EnglishName = "Natural",
                    Description = "シャープやフラットの効果を無効にし、元の音（幹音）に戻すことを示す記号（♮）。",
                    Aliases = { "♮" }
                },
                new MusicTerm {
                    JapaneseName = "全音", EnglishName = "Whole Tone",
                    Description = "半音二つ分の音程。",
                    Aliases = { "WholeTone" }
                },
                new MusicTerm {
                    JapaneseName = "半音", EnglishName = "Semitone",
                    Description = "西洋音楽における音程の最小単位。ピアノの隣り合う鍵盤（白鍵と黒鍵）の音程。",
                    Aliases = { "Semitone" }
                },
                new MusicTerm {
                    JapaneseName = "オクターブ", EnglishName = "Octave",
                    Description = "ある音に対して、周波数がちょうど2倍または1/2になる音との音程。ドから次のドまで。",
                    Aliases = { "Octave" }
                },
                new MusicTerm {
                    JapaneseName = "譜面", EnglishName = "Score",
                    Description = "音楽を五線譜などの記号で視覚的に記録したもの。楽譜。",
                    Aliases = { "Score", "楽譜" }
                },
                new MusicTerm {
                    JapaneseName = "音符", EnglishName = "Note",
                    Description = "音の高さと長さを表す記号。",
                    Aliases = { "Note" }
                },
                new MusicTerm {
                    JapaneseName = "休符", EnglishName = "Rest",
                    Description = "音を休止する長さ（休む長さ）を表す記号。",
                    Aliases = { "Rest" }
                },
                new MusicTerm {
                    JapaneseName = "4分音符", EnglishName = "Quarter Note",
                    Description = "4/4拍子において1拍の長さを持つ音符。",
                    Aliases = { "QuarterNote" }
                },
                new MusicTerm {
                    JapaneseName = "8分音符", EnglishName = "Eighth Note",
                    Description = "4/4拍子において半拍（0.5拍）の長さを持つ音符。",
                    Aliases = { "EighthNote" }
                },
                new MusicTerm {
                    JapaneseName = "16分音符", EnglishName = "Sixteenth Note",
                    Description = "4/4拍子において1/4拍（0.25拍）の長さを持つ音符。",
                    Aliases = { "SixteenthNote" }
                },
                new MusicTerm {
                    JapaneseName = "付点音符", EnglishName = "Dotted Note",
                    Description = "元の音符の1.5倍の長さを持つ音符。",
                    Aliases = { "DottedNote" }
                },
                new MusicTerm {
                    JapaneseName = "連符", EnglishName = "Tuplet",
                    Description = "1拍を通常とは異なる数（例：3, 5, 6）で等分割する音符。3連符が代表的。",
                    Aliases = { "Tuplet", "3連符" }
                },
                new MusicTerm {
                    JapaneseName = "タイ", EnglishName = "Tie",
                    Description = "同じ高さの二つの音符を結び、一つの音符として演奏することを示す記号。",
                    Aliases = { "Tie" }
                },
                new MusicTerm {
                    JapaneseName = "スラー", EnglishName = "Slur",
                    Description = "高さの異なる複数の音符を結び、滑らかに演奏すること（レガート）を示す記号。",
                    Aliases = { "Slur" }
                },
                new MusicTerm {
                    JapaneseName = "スタッカート", EnglishName = "Staccato",
                    Description = "音符の長さを短く切り、歯切れよく演奏することを示す記号。",
                    Aliases = { "Staccato" }
                },
                new MusicTerm {
                    JapaneseName = "テヌート", EnglishName = "Tenuto",
                    Description = "音符の長さを十分に保って演奏することを示す記号。",
                    Aliases = { "Tenuto" }
                },
                new MusicTerm {
                    JapaneseName = "アクセント", EnglishName = "Accent",
                    Description = "特定の音を目立たせて強く演奏することを示す記号。",
                    Aliases = { "Accent" }
                },
                new MusicTerm {
                    JapaneseName = "フェルマータ", EnglishName = "Fermata",
                    Description = "音符や休符を、記譜された長さよりも適度に延長して演奏することを示す記号。",
                    Aliases = { "Fermata" }
                },
                new MusicTerm {
                    JapaneseName = "クレッシェンド", EnglishName = "Crescendo",
                    Description = "だんだん強く（音量を大きく）演奏することを示す記号。",
                    Aliases = { "Cresc" }
                },
                new MusicTerm {
                    JapaneseName = "デクレッシェンド", EnglishName = "Decrescendo",
                    Description = "だんだん弱く（音量を小さく）演奏することを示す記号。ディミヌエンド（Diminuendo）とも言う。",
                    Aliases = { "Decresc", "Diminuendo" }
                },
                new MusicTerm {
                    JapaneseName = "フォルテ", EnglishName = "Forte",
                    Description = "強く演奏することを示す強弱記号（f）。",
                    Aliases = { "f" }
                },
                new MusicTerm {
                    JapaneseName = "ピアノ", EnglishName = "Piano",
                    Description = "弱く演奏することを示す強弱記号（p）。",
                    Aliases = { "p" }
                },
                new MusicTerm {
                    JapaneseName = "メゾフォルテ", EnglishName = "Mezzo Forte",
                    Description = "やや強く演奏することを示す強弱記号（mf）。",
                    Aliases = { "mf" }
                },
                new MusicTerm {
                    JapaneseName = "メゾピアノ", EnglishName = "Mezzo Piano",
                    Description = "やや弱く演奏することを示す強弱記号（mp）。",
                    Aliases = { "mp" }
                },
                new MusicTerm {
                    JapaneseName = "レガート", EnglishName = "Legato",
                    Description = "音と音の間が途切れないように、滑らかに演奏する技法。",
                    Aliases = { "Legato" }
                },
                new MusicTerm {
                    JapaneseName = "ピチカート", EnglishName = "Pizzicato",
                    Description = "ヴァイオリンなどの弦楽器の弦を、弓ではなく指で弾いて音を出す演奏技法。",
                    Aliases = { "Pizzicato" }
                },
                new MusicTerm {
                    JapaneseName = "トリル", EnglishName = "Trill",
                    Description = "主要な音符と、その2度上（全音または半音上）の音を素早く交互に演奏する装飾技法。",
                    Aliases = { "Trill" }
                },
                new MusicTerm {
                    JapaneseName = "グリッサンド", EnglishName = "Glissando",
                    Description = "ある音から別の音へ移る際に、間の音を滑らせるように（音階を素早く駆け上がる/下がるように）演奏する技法。",
                    Aliases = { "Glissando" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIキーボード", EnglishName = "MIDI Keyboard",
                    Description = "MIDI信号を出力するための鍵盤楽器。DAWに演奏情報を入力するために使われる。",
                    Aliases = { "MIDI Keyboard" }
                },
                new MusicTerm {
                    JapaneseName = "MIDIコントローラー", EnglishName = "MIDI Controller",
                    Description = "フェーダーやノブ、パッドなどを備え、DAWのミキサー操作やシンセサイザーのパラメータ変更をMIDI信号で行うための機器。",
                    Aliases = { "MIDI Controller" }
                },
                new MusicTerm {
                    JapaneseName = "モニタースピーカー", EnglishName = "Monitor Speaker",
                    Description = "音楽制作用に設計された、原音を忠実に再生するためのスピーカー。",
                    Aliases = { "MonitorSpeaker" }
                },
                new MusicTerm {
                    JapaneseName = "ヘッドホン", EnglishName = "Headphones",
                    Description = "耳を覆って音声を聞くための機器。ミキシング用にはモニターヘッドホンが使われる。",
                    Aliases = { "Headphones" }
                },
                new MusicTerm {
                    JapaneseName = "マイク", EnglishName = "Microphone",
                    Description = "音（空気の振動）を電気信号に変換する機器。ボーカルや楽器の録音に使う。",
                    Aliases = { "Mic", "マイクロフォン" }
                },
                new MusicTerm {
                    JapaneseName = "ダイナミックマイク", EnglishName = "Dynamic Microphone",
                    Description = "比較的丈夫で、大きな音圧にも耐えられるマイク。ライブやドラムの録音によく使われる。",
                    Aliases = { "DynamicMic" }
                },
                new MusicTerm {
                    JapaneseName = "コンデンサーマイク", EnglishName = "Condenser Microphone",
                    Description = "非常に感度が高く、微細な音まで捉えられるマイク。ボーカルやアコースティック楽器のスタジオ録音によく使われる。動作に電源（ファンタム電源）が必要。",
                    Aliases = { "CondenserMic" }
                },
                new MusicTerm {
                    JapaneseName = "ファンタム電源", EnglishName = "Phantom Power",
                    Description = "コンデンサーマイクを動作させるために、オーディオインターフェースやミキサーから供給される電源（通常+48V）。",
                    Aliases = { "PhantomPower", "+48V" }
                },
                new MusicTerm {
                    JapaneseName = "プリアンプ", EnglishName = "Preamplifier",
                    Description = "マイクが捉えた微弱な電気信号を、適切なレベルまで増幅する機器。",
                    Aliases = { "Preamplifier", "ヘッドアンプ" }
                },
                new MusicTerm {
                    JapaneseName = "アウトボード", EnglishName = "Outboard Gear",
                    Description = "DAW内部のプラグインではなく、物理的な（ハードウェアの）コンプレッサー、EQ、リバーブなどのエフェクト機器。",
                    Aliases = { "Outboard" }
                }
            };
        }

        public static MusicTerm? SearchTerm(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            query = query.ToLower();
            var terms = MidiMusicCompletionSettings.Default.TermsList;

            return terms.FirstOrDefault(t => t.JapaneseName.ToLower() == query ||
                                           t.EnglishName.ToLower() == query ||
                                           t.Aliases.Any(a => a.ToLower() == query));
        }

        public static List<MusicTerm> GetCompletions(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return new List<MusicTerm>();
            prefix = prefix.ToLower();
            bool fuzzy = MidiMusicCompletionSettings.Default.EnableFuzzySearch;
            int max = MidiMusicCompletionSettings.Default.MaxSuggestions;
            var terms = MidiMusicCompletionSettings.Default.TermsList;

            return terms
                .Where(t => t.JapaneseName.ToLower().StartsWith(prefix) ||
                            t.EnglishName.ToLower().StartsWith(prefix) ||
                            t.Aliases.Any(a => a.ToLower().StartsWith(prefix)) ||
                            (fuzzy && (t.JapaneseName.ToLower().Contains(prefix) ||
                                      t.EnglishName.ToLower().Contains(prefix) ||
                                      t.Aliases.Any(a => a.ToLower().Contains(prefix)))))
                .OrderBy(t => GetMatchScore(t, prefix))
                .Take(max)
                .ToList();
        }

        private static int GetMatchScore(MusicTerm term, string prefix)
        {
            prefix = prefix.ToLower();
            if (term.JapaneseName.ToLower().StartsWith(prefix)) return 0;
            if (term.EnglishName.ToLower().StartsWith(prefix)) return 1;
            if (term.Aliases.Any(a => a.ToLower().StartsWith(prefix))) return 2;
            if (term.JapaneseName.ToLower().Contains(prefix)) return 3;
            if (term.EnglishName.ToLower().Contains(prefix)) return 4;
            if (term.Aliases.Any(a => a.ToLower().Contains(prefix))) return 5;
            return 10;
        }
    }
}