using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public enum KokoFileOperationKind
    {
        ReadText,
        WriteText,
        CreateDirectory,
        Delete,
        Move
    }

    public sealed class KokoFileOperationRequest
    {
        public KokoFileOperationKind Kind { get; set; }
        public string Path { get; set