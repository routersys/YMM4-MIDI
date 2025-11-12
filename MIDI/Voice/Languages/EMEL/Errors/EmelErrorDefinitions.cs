using System.Collections.Generic;

namespace MIDI.Voice.EMEL.Errors
{
    public enum EmelErrorCode
    {
        Lexer_InvalidCharacter,
        Lexer_UnterminatedString,

        Parser_UnexpectedToken,
        Parser_ExpectedToken,
        Parser_InvalidAssignmentTarget,
        Parser_ExpectedExpression,

        Runtime_Unexpected,
        Runtime_UndefinedVariable,
        Runtime_UndefinedFunction,
        Runtime_WrongArgumentCount,
        Runtime_InvalidType,
        Runtime_InvalidTypeForOperation,
        Runtime_InvalidTypeForRepeat,
        Runtime_DivideByZero,
        Runtime_ValueOutOfRange,

        Compiler_Unexpected,
        Compiler_MissingEmelDeclaration,
        Compiler_InvalidEmelDeclaration
    }

    public static class EmelErrors
    {
        private static readonly Dictionary<EmelErrorCode, string> ErrorFormats = new Dictionary<EmelErrorCode, string>
        {
            { EmelErrorCode.Lexer_InvalidCharacter, "無効な文字 '{0}' が見つかりました。" },
            { EmelErrorCode.Lexer_UnterminatedString, "文字列リテラルが閉じられていません。" },

            { EmelErrorCode.Parser_UnexpectedToken, "予期しないトークン '{0}' が見つかりました。" },
            { EmelErrorCode.Parser_ExpectedToken, "トークン '{0}' が必要ですが、'{1}' が見つかりました。" },
            { EmelErrorCode.Parser_InvalidAssignmentTarget, "代入先が不正です。変数名を指定してください。" },
            { EmelErrorCode.Parser_ExpectedExpression, "式が必要です。" },

            { EmelErrorCode.Runtime_Unexpected, "予期しないランタイムエラーが発生しました: {0}" },
            { EmelErrorCode.Runtime_UndefinedVariable, "未定義の変数 '{0}' が参照されました。" },
            { EmelErrorCode.Runtime_UndefinedFunction, "未定義の関数 '{0}' が呼び出されました。" },
            { EmelErrorCode.Runtime_WrongArgumentCount, "関数 '{0}' は {1} 個の引数を必要としますが、{2} 個指定されました。" },
            { EmelErrorCode.Runtime_InvalidType, "操作に対して型 '{0}' が不正です。" },
            { EmelErrorCode.Runtime_InvalidTypeForOperation, "{0} 操作は '{1}' と '{2}' 型の間では実行できません。" },
            { EmelErrorCode.Runtime_InvalidTypeForRepeat, "repeat の回数は数値である必要があります。" },
            { EmelErrorCode.Runtime_DivideByZero, "0による除算は許可されていません。" },
            { EmelErrorCode.Runtime_ValueOutOfRange, "{0} の値 '{1}' は、範囲 ({2} ~ {3}) 外です。" },

            { EmelErrorCode.Compiler_Unexpected, "コンパイラ内部で予期しないエラーが発生しました: {0}" },
            { EmelErrorCode.Compiler_MissingEmelDeclaration, "コードの1行目に #!EMEL または #!EMEL2 宣言が必要です。" },
            { EmelErrorCode.Compiler_InvalidEmelDeclaration, "1行目のEMEL宣言 '{0}' は無効です。#!EMEL または #!EMEL2 を使用してください。" }
        };

        public static string Format(EmelErrorCode code, params object[] args)
        {
            if (ErrorFormats.TryGetValue(code, out var format))
            {
                try
                {
                    return string.Format(format, args);
                }
                catch (System.Exception)
                {
                    return format;
                }
            }
            return "不明なエラーが発生しました。";
        }
    }
}