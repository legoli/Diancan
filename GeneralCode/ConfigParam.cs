using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralCode
{
    public class ConfigParam
    {
        public Dictionary<String, String> system_param = null;
        public List<String> queue_order_change_reason = null;
        
        public ConfigParam()
        {
            CXml xmldoc = new CXml();
            system_param = xmldoc.getSystemParam();
            queue_order_change_reason = xmldoc.getQueueReason();
        }
    }
}
