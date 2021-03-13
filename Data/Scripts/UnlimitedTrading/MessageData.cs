using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnlimitedTrading;

namespace UnlimitedTrading
{
    [ProtoContract]
    public class MessageData
    {
        [ProtoMember(1)]
        public string Sender;

        [ProtoMember(2)]
        public string Message;

        [ProtoMember(3)]
        public string Type;

        [ProtoMember(4)]
        public ulong SteamId;

        [ProtoMember(5)]
        public string DialogTitle;
    }
}
