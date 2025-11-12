using ComputeSharp;

namespace MIDI.GPU
{
    public struct GpuNote
    {
        public Float2 Position;
        public Float2 Size;
        public Float4 Color;
        public Float4 BorderColor;
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct NoteRenderShader : IComputeShader<float4>
    {
        private readonly ReadOnlyBuffer<GpuNote> notes;
        private readonly int noteCount;
        private readonly float2 scrollOffset;

        public NoteRenderShader(ReadOnlyBuffer<GpuNote> notes, int noteCount, float2 scrollOffset)
        {
            this.notes = notes;
            this.noteCount = noteCount;
            this.scrollOffset = scrollOffset;
        }

        public float4 Execute()
        {
            float2 worldPos = ThreadIds.XY + scrollOffset;

            for (int i = 0; i < noteCount; i++)
            {
                GpuNote note = notes[i];
                if (worldPos.X >= note.Position.X && worldPos.X < note.Position.X + note.Size.X &&
                    worldPos.Y >= note.Position.Y && worldPos.Y < note.Position.Y + note.Size.Y)
                {
                    if (worldPos.X < note.Position.X + 1 || worldPos.X >= note.Position.X + note.Size.X - 1 ||
                        worldPos.Y < note.Position.Y + 1 || worldPos.Y >= note.Position.Y + note.Size.Y - 1)
                    {
                        return note.BorderColor;
                    }
                    return note.Color;
                }
            }

            return new float4(0, 0, 0, 0);
        }
    }
}
