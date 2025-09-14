# MIDI ファイル読み込みプラグイン for YMM4

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/YMM4-MIDI.svg)](https://github.com/routersys/YMM4-MIDI/releases)

YMM4（YukkuriMovieMaker v4）で、MIDIファイル（`.mid`, `.midi`）を音声アイテムとして読み込めるようにするファイルソースプラグインです。

![image](https://github.com/routersys/YMM4-MIDI/blob/main/MIDI.png)

---

### 概要

このプラグインを導入すると、MIDIファイルをタイムラインに直接ドラッグ＆ドロップするだけで、音声アイテムとして利用できるようになります。同梱されているSoundFontファイル（`GeneralUser-GS.sf2`）を使ってMIDIを音声に変換（シンセサイズ）するため、豊かな楽器の音色で再生することが可能です。

もしSoundFontファイルが見つからない場合は、代替機能として簡易的な正弦波で音を鳴らします。

### 主な機能

- **MIDIファイルのサポート**:
    - `.mid`および`.midi`形式のファイルを音声アイテムとしてタイムラインに追加できます。

- **SoundFontによるシンセサイズ**:
    - プラグインと同じフォルダ内にある`.sf2`ファイル（SoundFont）を自動で読み込み、高品質な音源でMIDIをレンダリングします。
    - デフォルトで高音質な`GeneralUser-GS.sf2`を同梱しています。
    - お好みのSoundFontファイルに入れ替えて、音色をカスタマイズすることも可能です。

- **フォールバック機能**:
    - `.sf2`ファイルが見つからない場合でも、純粋な正弦波（サイン波）を生成してMIDIを再生します。

---

### 導入方法

1. **[リリースページ](https://github.com/routersys/YMM4-MIDI/releases)** から最新版の `.ymme` ファイルをダウンロードします。
2. ダウンロードしたファイルを実行（ダブルクリック）してインストールを開始します。
3. YMM4にプラグインが登録され、インストール完了です。

### 使い方
1. タイムラインに音声ファイルとして`.mid`または`.midi`ファイルをドラッグ＆ドロップします。
2. または、「音声アイテム」からMIDIファイルを読み込みます。
> [!NOTE]
> ファイル選択ダイアログでMIDIファイルが表示されない場合は、右下のファイル形式フィルターを「すべてのファイル (*.*)」に変更してください。
3. ファイルが自動的に音声アイテムとしてタイムラインに追加され、再生できるようになります。
> [!CAUTION]
> 再生までに少し時間がかかります。

### 免責事項

このプラグインを使用したことによって生じた、いかなる損害についても作者は責任を負いません。自己責任でご利用ください。

### クレジット
GeneralUser GS v2.0.1使用
   - 作者：S. Christian Collins
   - ウェブサイト：http://www.schristiancollins.com

### リリースノート
[2025/09/14] v1.0.0
- 初回リリース。
