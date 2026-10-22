using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using MessagePack;
using MessagePack.Formatters;
using dc.haxe.io;
using dc.hxbit;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;

namespace DeadCellsMultiplayerX.Common.Serializers
{
    public sealed class HxbitMessagePackFormatter<T> : IMessagePackFormatter<T?>
        where T : HaxeObject
    {
        private static readonly ThreadLocal<Serializer> SerializerPool = new(() => new Serializer());

        private readonly dc.hl.Class manualHxClass;
        private readonly int manualClid;

        public HxbitMessagePackFormatter(dc.hl.Class hxClass, int clid)
        {
            manualHxClass = hxClass;
            manualClid = clid;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var binSeq = reader.ReadBytes();
            if (binSeq == null)
                return default;

            Serializer ser = SerializerPool.Value!;
            dc.hl.Class hxCls = manualHxClass;
            int clid = manualClid;

            if (binSeq.Value.IsSingleSegment)
            {
                ReadOnlySpan<byte> span = binSeq.Value.FirstSpan;
                unsafe
                {
                    fixed (byte* p = span)
                    {
                        Bytes bytes = new Bytes((IntPtr)p, span.Length);
                        ser.beginLoad(bytes, default);
                        T? result = (T?)ser.getRef(hxCls, clid);
                        ser.endLoad();
                        return result;
                    }
                }
            }
            else
            {
                byte[] merged = binSeq.Value.ToArray();
                unsafe
                {
                    fixed (byte* p = merged)
                    {
                        Bytes bytes = new Bytes((IntPtr)p, merged.Length);
                        ser.beginLoad(bytes, default);
                        T? result = (T?)ser.getRef(hxCls, clid);
                        ser.endLoad();
                        return result;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Serialize(ref MessagePackWriter writer, T? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            Serializer ser = SerializerPool.Value!;
            ser.beginSave();
            ser.addKnownRef(
                value.ToVirtual<virtual___uid_getCLID_getSerializeSchema_serialize_unserialize_unserializeInit_>()
            );

            Bytes data = ser.endSave(default);

            unsafe
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>((void*)data.b, data.length);
                writer.Write(span);
            }
        }
    }
}