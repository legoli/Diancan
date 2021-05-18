using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace GeneralCode
{
    /**
     * 把指定的DJLSH登记到超声科
     * 条件限制:只针对体检类型为天方达登记的超声检查
     * 
     * **/
    public class RegisterToUltraSound
    {
        private String m_djlsh = "";//待检流水号
        private String m_exam_type="体检中心";//如果为"本部"则需要超声科叫号

        //构造函数 
        public RegisterToUltraSound(String djlsh, String examType)
        {
            m_djlsh = CSTR.trim(djlsh);
            m_exam_type = CSTR.trim(examType);
        }

        public bool register()
        {
            if (CSTR.isEmpty(m_djlsh)) return false;
            if (CSTR.isEmpty(m_exam_type)) m_exam_type = "体检中心";

            String webAPI_link = String.Format("http://192.168.3.206/api/webapi/tjRegister?tjh={0}&local={1}", m_djlsh, m_exam_type);
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(webAPI_link);
            request.Method = "GET";

            using (WebResponse response = request.GetResponse())
            {
                String txt = response.GetResponseStream().ToString();
            }

            return false;
        }
    }
}
