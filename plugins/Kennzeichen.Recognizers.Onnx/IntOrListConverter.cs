using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Kennzeichen.Recognizers.Onnx;

/// <summary>Liest <c>padding_color</c>, das in fast-plate-ocr entweder eine Zahl oder eine Liste sein darf.</summary>
internal sealed class IntOrListConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(List<int>);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return new List<int> { int.Parse(scalar.Value, System.Globalization.CultureInfo.InvariantCulture) };
        }

        var values = new List<int>();
        parser.Consume<SequenceStart>();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            values.Add(int.Parse(parser.Consume<Scalar>().Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return values;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
        throw new NotSupportedException();
}
