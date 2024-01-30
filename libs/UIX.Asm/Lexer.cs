using Sprache;
using System.Collections.Generic;
using System.Linq;

namespace UIX.Asm;

public static class Lexer
{
    public static readonly Parser<string> AlphanumericText = Parse.LetterOrDigit.AtLeastOnce().Text();

    public static readonly Parser<string> Uri = Parse.LetterOrDigit.Or(Parse.Chars(":/!._-")).AtLeastOnce().Text();

    public static readonly Parser<string> StatementEnd = Parse.Char(';').Return(";").Or(Parse.LineTerminator);

    public static readonly Parser<Import> Import =
        from _ in Parse.String(".import").Token()
        from uri in Uri.Token()
        from __ in Parse.String("as").Token()
        from name in AlphanumericText
        from end in StatementEnd
        select new Import(uri, name);

    public static readonly Parser<IBodyItem> BodyItem = ParseBodyItem;

    public static readonly Parser<Program> Program =
        from imports in Import.Token().Many()
        from body in BodyItem.Many()
        select new Program(imports, body);

    private static IResult<IBodyItem> ParseBodyItem(IInput input)
    {
        var trimWhitespaceResult = Parse.WhiteSpace.Many()(input);
        input = trimWhitespaceResult.Remainder;

        var identifierResult = Parse.Letter.AtLeastOnce().Text().Invoke(input);
        if (!identifierResult.WasSuccessful)
            return Result.Failure<Instruction>(input, "Invalid code", System.Array.Empty<string>());

        input = identifierResult.Remainder;
        var identifier = identifierResult.Value;
        IBodyItem bodyItem;

        if (!input.AtEnd && input.Current == ':')
        {
            input = input.Advance();
            bodyItem = new Label(identifier);
            input = StatementEnd(input).Remainder;
        }
        else
        {
            List<Operand> operands = new();
            var endOfInstructionResult = StatementEnd(input);
            input = endOfInstructionResult.Remainder;

            if (!endOfInstructionResult.WasSuccessful)
            {
                input = Parse.WhiteSpace.Many()(input).Remainder;

                var operandsResult = Parse.Ref(() => AlphanumericText).DelimitedBy(Parse.Char(',').Token())(input);
                operands.AddRange(operandsResult.Value.Select(s => new Operand(s)));
                input = operandsResult.Remainder;
            }

            bodyItem = new Instruction(identifier, operands);
        }

        return Result.Success(bodyItem, input);
    }
}
