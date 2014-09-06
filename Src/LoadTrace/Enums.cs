using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LoadTrace
{
   
    public enum  TraceEventClass
    {
        TraceStart = 65534,
        TraceStop = 65533,
        FirstFile = 65528,
        TraceRollover = 65527,
        AuditLogin = 14,
        ExistingConnection = 17
    }

}
