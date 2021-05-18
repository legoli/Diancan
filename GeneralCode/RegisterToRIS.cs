using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeneralCode
{
    /**
     * 把指定的IndexID登记到放射科
     * 条件限制:只针对体检类型为天方达登记的放射科检查
     * 
     * **/

    public class RegisterToRIS
    {
        //配置列表
        //设备对应表
        private Dictionary<String, String> mapModality = new Dictionary<string, string>()
        {
            //[key]JiaohaoTable.ExamType : [value]RIS.ModalityID
            {"07","151"},//PhilipsDR
            {"15","152"},//SimensCT
            {"24","154"},//DR
            {"53","151"},//PhilipsDR
            {"57","151"}//PhilipsDR
        };
        //检查方法对应表
        private Dictionary<String, String> mapExamMethod = new Dictionary<string, string>()
        {
            //[key]JiaohaoTable.ProcedureStepID : [value]RIS.ProcedureStepID
            {"D191707",""},//胸部正位片
            {"0706",""},//胸部正位DR摄影/加片
            {"0762",""},//胸部正位片（DR）
            {"0763",""},//胸部正位片（职业体检）
            {"1502",""},//胸部CT平扫
            {"1518",""},//上腹部CT平扫
            {"2401",""}//乳腺钼靶片
        };
    }
}
