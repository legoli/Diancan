using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace GeneralCode
{
    public class RuleForRegister
    {
        //检查登记的三个个层次:Study(Patient) -> Exam(Order) -> Procedure(item)
        //Study: 本次登记的同一手环的所有检查记录
        //Exam(Order): Study中的其中一个检查
        //Procedure:检查方法,Exam可以由一个或多个Procedure组成,存放与JiaohaoExamInfo
        
        //定义本登记的类型
        private bool REGISTER_TYPE_IS_TFD = false;

        //保存本次登记的Study记录
        /// 注意:reg_tbl不是JiaohaoTable格式,而是从天方达中查找到的记录的格式
        public DataTable register_table = null;
        String rfid_no = "";
        String ring_no = "";
        String tfd_DJLSH = "";//天方达的检查DJLSH
        //[RoomID]:register_table.CheckRoom
        //天方达体检项目与手工项目的区分:register_table.DWMC='非天方达'

        //定义无效检查房间
        //天方达数据库中的"一般诊疗费"等检查项目也已经设为[无效检查]
        public const String VALID_ROOM_ID = "[无效检查]";

        //定义相关联的房间串
        String endingRoomCluster_full = "[308][309][310][311][312][313]";
        String endingRoomCluster_normal = "[309][310][311]";
        String endingRoomCluster_vocation = "[308][312][313]";

        #region <BEGIN>这是手环配发时调用的方法,用于综合分析所有的项目,归类合并房间,选择一个最优房间
        /// <summary>
        /// 这是手环配发时调用的方法,用于综合分析所有的项目,归类合并房间,选择一个最优房间
        /// </summary>
        /// <param name="reg_tbl">登记台发送的体检系统Exam信息或Speicail的Exam信息</param>
        /// <returns>返回的格式:map[1]=[307]</returns>
        /// map[0]=[307] 表示原有的Exam记录中第一[0]条记录的房间号[306][307]指定为[307]
        /// map[1]=[311] 表示原有的Exam记录中第二[1]条记录的房间号[308][309]指定为[311]

        //获取Study的Exams的分类和状态
        private bool STUDY_is_vacation_exam = false;
        private bool STUDY_has_312_313_room = false;
        private bool STUDY_has_310_alone = false;
        private bool STUDY_has_311_alone = false;
        private bool STUDY_has_311_or_310_alone = false;

        public RuleForRegister(String strDJLSH, String rfid, String ringNo)
        {
            //此构造函数为天方达检查登记专用
            REGISTER_TYPE_IS_TFD = true;

            tfd_DJLSH = strDJLSH;
            rfid_no = rfid;
            ring_no = ringNo;
        }

        public RuleForRegister(DataTable reg_tbl, String rfid, String ringNo)
        {
            //此构造函数为手工登记专用
            REGISTER_TYPE_IS_TFD = false;

            register_table = reg_tbl;
            rfid_no = rfid;
            ring_no = ringNo;
        }

        public void TFD_set_Register_table(DataTable reg_tbl)
        {
            //此构造函数为天方达检查登记专用
            register_table = reg_tbl;
        }

        public List<String> parse_and_allot_RoomID_for_STUDY()
        {
            //[RoomID]:register_table.CheckRoom

            if (CSTR.IsTableEmpty(register_table)) return null;

            //记录处理的exam数目
            int examCount = register_table.Rows.Count;

            //获取Study的Exams的分类和状态(不检查RoomInfo.active)
            STUDY_is_vacation_exam = StudyIsVocationType();
            STUDY_has_312_313_room = StudyHas_312_313_Room();
            STUDY_has_310_alone = StudyHas_310_alone();
            STUDY_has_311_alone = StudyHas_311_alone();
            if (STUDY_has_310_alone || STUDY_has_311_alone) STUDY_has_311_or_310_alone = true;

            //第一轮parse返回的map,经过对属于总检房间的处理后的RoomID列表
            List<String> list_round_1 = parse_1_Round_Ending_Room_Handle();
            if (null == list_round_1) return null;
            if (list_round_1.Count != examCount) return null;//保证返回的数量和记录数量一致

            //第二轮parse返回map,经过对其他房间的处理后的RoomID列表
            List<String> list_round_2 = parse_2_Round_Other_Room_Handle(list_round_1);
            if (null == list_round_2) return null;
            if (list_round_2.Count != examCount) return null;//保证返回的数量和记录数量一致

            //第3轮parse返回map,确保房间Active
            List<String> list_round_3 = parse_3_Round_Active_Room_Handle(list_round_2);
            if (null == list_round_3) return null;
            if (list_round_3.Count != examCount) return null;//保证返回的数量和记录数量一致

            //第三轮parse返回map,对同一个EXAM的多个房间,选择返回一个未完成检查数最少的房间
            List<String> list_round_4 = parse_4_Round_Recommanded_Room_Handle(list_round_3);
            if (null == list_round_4) return null;
            if (list_round_4.Count != examCount) return null;//保证返回的数量和记录数量一致

            //第四轮parse返回map,同类型房间重复出现时,只选择唯一的一个,避免重复
            List<String> list_round_5 = parse_5_Round_RoomType_Uniqueness_Handle(list_round_4);
            if (null == list_round_5) return null;
            if (list_round_5.Count != examCount) return null;//保证返回的数量和记录数量一致

            return list_round_5;
        }

        /// <summary>
        /// parse_1_Round_Ending_Room_Handle
        /// 处理[308]~[313]的房间
        /// 根据职检与否来更改房间指向
        /// 保证检查有效性和房间号不为空
        /// </summary>
        /// <returns>返回修改了相关总检房间的列表,如[309][311]保存为list[2]='[308][312][313]'</returns>
        private List<String> parse_1_Round_Ending_Room_Handle()
        {
            //[RoomID]:register_table.CheckRoom

            //获取各类总检房间的开启情况
            String active_endingRoomCluster_full = get_active_RoomID_Cluster(endingRoomCluster_full);
            String active_endingRoomCluster_normal = get_active_RoomID_Cluster(endingRoomCluster_normal);
            String active_endingRoomCluster_vocation = get_active_RoomID_Cluster(endingRoomCluster_vocation);

            bool is_all_room_closed = CSTR.isEmpty(active_endingRoomCluster_full);
            bool is_all_normal_room_closed = CSTR.isEmpty(active_endingRoomCluster_normal);
            bool is_all_vocation_room_closed = CSTR.isEmpty(active_endingRoomCluster_vocation);

            //逐一处理每个检查的房间串,并处理返回结果
            List<String> roomList = new List<string>();
            foreach (DataRow row in register_table.Rows)
            {
                //[规则]检查类型和项目编号等四个关键字段为空则不予登记
                String OrderProcedureName = CSTR.ObjectTrim(row["ExamTypeMC"]);
                String ProcedureStepID = CSTR.ObjectTrim(row["TJXMBH"]);
                String ProcedureStepName = CSTR.ObjectTrim(row["TJXMMC"]);
                String ExamType = CSTR.ObjectTrim(row["ExamType"]);
                if (CSTR.isEmpty(ExamType) || CSTR.isEmpty(OrderProcedureName) ||
                    CSTR.isEmpty(ProcedureStepID) || CSTR.isEmpty(ProcedureStepName))
                {
                    roomList.Add(VALID_ROOM_ID);
                    continue;
                }

                //[规则]对于没有CheckRoom的记录,属于体检系统新增记录,房间并入[other]系列
                String rooms_id = CSTR.ObjectTrim(row["CheckRoom"]);
                String[] room_arr = CSTR.splitRooms(rooms_id);
                if (null == room_arr || room_arr.Length <= 0)
                {
                    roomList.Add("[other]");
                    continue;
                }

                //判断此exam的房间串是否属于endingRoom中的一个
                bool is_belong_endingRoom = false;//总检房(总)
                bool is_belong_normal_endingRoom = false;//个检总检
                bool is_belong_vocation_endingRoom = false;//职检总检
                foreach (String roomID in room_arr)
                {
                    if (endingRoomCluster_full.IndexOf(roomID) >= 0) is_belong_endingRoom = true;
                    if (endingRoomCluster_normal.IndexOf(roomID) >= 0) is_belong_normal_endingRoom = true;
                    if (endingRoomCluster_vocation.IndexOf(roomID) >= 0) is_belong_vocation_endingRoom = true;
                }

                //如果不属于总检房间类型,写回原房间串,直接处理下一个exam
                if (false == is_belong_endingRoom)
                {
                    roomList.Add(rooms_id);
                    continue;
                }

                //----------------------------总检房间类型的处理---------------------
                //[规则]全部总检房间都关闭时,个检选择[311],职检选择[313]
                if (is_all_room_closed)
                {
                    String strSel = "[311]";
                    if (STUDY_is_vacation_exam) strSel = "[313]";

                    roomList.Add(strSel);
                    continue;
                }

                //[规则]如果有独立的[311]或[310]房间的总检房间,直接映射为[311](包括职检)
                if (STUDY_has_311_or_310_alone)
                {
                    //注意:本循环中的rooms_id并非一定为[310]或[311],甚至可能为[312][313]
                    if (DatabaseCache.RoomInfo_is_active("[311]"))
                    {
                        //如果311房间active,直接选择
                        roomList.Add("[311]");
                        continue;
                    }
                }

                //[规则]如果Study中有职检EndingRoom,或者存在职检的项目,直接映射到[308][312][313],否则映射到[309][311]
                if (STUDY_has_312_313_room || STUDY_is_vacation_exam)
                {
                    //如果当前exam的rooms_id属于[312][313]则直接写回原串,否则强制写入[312][313]
                    if (is_belong_vocation_endingRoom)
                    {
                        roomList.Add(rooms_id);
                        continue;
                    }
                    else
                    {
                        roomList.Add("[308][312][313]");
                        continue;
                    }
                }


                //其他的个检总检房间,写入原串中的active房间
                //(当前条件:1.非职检 2.职检与个检必有一个是有未关闭房间
                String strActive = get_active_RoomID_Cluster(rooms_id);
                String strSelect = strActive;
                if (CSTR.isEmpty(strActive))
                {
                    if (false == is_all_normal_room_closed)
                    {
                        strSelect = active_endingRoomCluster_normal;
                    }
                    else
                    {
                        strSelect = active_endingRoomCluster_vocation;
                    }
                }
                roomList.Add(strSelect);

            }//end foreach

            return roomList;
        }

        /// <summary>
        /// 处理[306][307]职检->[307](取消职检到[307])) : [309][311]女->[311]
        /// 此轮的返回结果可以作为多个房间的备选(保存到JiaohaoTable.OptionRooms字段)
        /// </summary>
        /// <param name="listRound_1">第一轮的处理结果</param>
        /// <returns></returns>
        private List<String> parse_2_Round_Other_Room_Handle(List<String> listRound_1)
        {
            //[RoomID]:register_table.CheckRoom

            //逐一处理每个检查的房间串,并处理返回结果
            List<String> roomList = new List<string>();

            int nCount = register_table.Rows.Count;
            for (int i = 0; i < nCount; i++)
            {
                //注: 由于Round_1保证了数据的完整性,在Round2无需进行CSTR.isEmpty的处理了
                String rooms_id_origin = "";
                String rooms_id_round_1 = "";

                try
                {
                    rooms_id_origin = CSTR.ObjectTrim(register_table.Rows[i]["CheckRoom"]);
                    rooms_id_round_1 = listRound_1[i];

                    //0:女 1:男 %:未知
                    String PatientGender = CSTR.ObjectTrim(register_table.Rows[i]["XB"]);
                    if (PatientGender.Equals("0")) PatientGender = "女"; else PatientGender = "男";

                    //获取round_1的房间类型
                    String[] rooms_arr_round_1 = CSTR.splitRooms(rooms_id_round_1);
                    bool has_UltraSound = false;
                    bool has_309 = false;
                    bool has_311 = false;
                    foreach (String roomID in rooms_arr_round_1)
                    {
                        if ("[306][307][401][402][403][404]".IndexOf(roomID) >= 0) has_UltraSound = true;
                        if (roomID.Equals("[309]")) has_309 = true;
                        if (roomID.Equals("[311]")) has_311 = true;
                    }

                    //[规则] 对于[309][311]的房间,男的选[309],女的选[311]
                    if (has_309 && has_311)
                    {
                        String strSel = rooms_id_round_1;

                        if (PatientGender.Equals("女"))
                        {
                            strSel = "[311]";
                        }
                        else
                        {
                            strSel = strSel.Replace("[311]", "");//去掉[311]房间
                            //如果为空则恢复原有串
                            if (CSTR.isEmpty(strSel))
                            {
                                strSel = rooms_id_round_1;
                            }
                        }

                        roomList.Add(strSel);
                        continue;
                    }
                    else if (has_UltraSound)
                    {
                        //[规则] (取消)职检的B超检查默认选[307],,经阴道检查[403]
                        String strSel = rooms_id_round_1;

                        //解决心电错误分配到[307]的问题
                        //if (STUDY_is_vacation_exam) 
                        //{
                        //    strSel = "[307]";
                        //}

                        roomList.Add(strSel);
                        continue;
                    }
                    else
                    {
                        //其他不无需处理的RoomID,直接写回原有的房间串
                        roomList.Add(rooms_id_round_1);
                    }
                }
                catch
                {
                    //如果还出错,直接写回Round1的串
                    String strSel = rooms_id_round_1;
                    if (CSTR.isEmpty(rooms_id_round_1))
                    {
                        strSel = rooms_id_origin;
                    }

                    roomList.Add(strSel);
                }

            }//end for

            return roomList;
        }

        /// <summary>
        /// 保证所有检查的房间都是active
        /// </summary>
        /// <param name="listRound_2">上一轮返回的房间串</param>
        /// <returns></returns>
        private List<String> parse_3_Round_Active_Room_Handle(List<String> listRound_2)
        {
            //[RoomID]:register_table.CheckRoom

            //逐一处理每个检查的房间串,并处理返回结果
            List<String> roomList = new List<string>();

            int nCount = register_table.Rows.Count;
            for (int i = 0; i < nCount; i++)
            {
                //注: 由于Round_1_2保证了数据的完整性,在Round3无需进行CSTR.isEmpty的处理了
                String rooms_id_origin = "";
                String rooms_id_round_2 = "";

                try
                {
                    rooms_id_origin = CSTR.ObjectTrim(register_table.Rows[i]["CheckRoom"]);
                    rooms_id_round_2 = listRound_2[i];

                    //如果round_2中有active的房间,直接作为返回结果
                    if (get_active_RoomID_Cluster(rooms_id_round_2).Length > 0)
                    {
                        roomList.Add(get_active_RoomID_Cluster(rooms_id_round_2));
                        continue;
                    }

                    //如果Round_2中的房间为disactive,则选择第一个,并判断房间类型,返回同类房间中激活的那一个
                    String[] rooms_arr_round_2 = CSTR.splitRooms(rooms_id_round_2);
                    if (null == rooms_arr_round_2 || rooms_arr_round_2.Length <= 0)
                    {
                        //不予登记
                        roomList.Add(VALID_ROOM_ID);
                        continue;  
                    }

                    String first_room_id = rooms_arr_round_2[0];//取第一个做房间类型判断条件

                    //获取各类总检房间的开启情况
                    String active_endingRoomCluster_full = get_active_RoomID_Cluster(endingRoomCluster_full);
                    String active_endingRoomCluster_normal = get_active_RoomID_Cluster(endingRoomCluster_normal);
                    String active_endingRoomCluster_vocation = get_active_RoomID_Cluster(endingRoomCluster_vocation);

                    String strSel = first_room_id;
                    if (endingRoomCluster_normal.IndexOf(first_room_id) >= 0 ||
                        "[315]".Equals(first_room_id) ||
                        "[317]".Equals(first_room_id))//[规则]315,317房间关闭时,自动到普通总检
                    {
                        //属于普通总检房间
                        if (active_endingRoomCluster_normal.Length > 0)
                        {
                            strSel = active_endingRoomCluster_normal;
                        }
                        else if (active_endingRoomCluster_full.Length > 0)
                        {
                            strSel = active_endingRoomCluster_full;
                        }
                    }
                    else if (endingRoomCluster_vocation.IndexOf(first_room_id) >= 0)
                    {
                        //属于职检总检房间
                        if (active_endingRoomCluster_vocation.Length > 0)
                        {
                            strSel = active_endingRoomCluster_vocation;
                        }
                        else if (active_endingRoomCluster_full.Length > 0)
                        {
                            strSel = active_endingRoomCluster_full;
                        }
                    }
                    else if ("[302][303][304][305][405]".IndexOf(first_room_id) >= 0)
                    {
                        String tempRoomID = get_active_RoomID_Cluster("[302][303][304][305][405]");
                        if (tempRoomID.Length > 0)
                        {
                            strSel = tempRoomID;
                        }
                    }
                    else if ("[306][307][401][402][403][404]".IndexOf(first_room_id) >= 0)
                    {
                        String tempRoomID = get_active_RoomID_Cluster("[306][307][401][402][403][404]");
                        if (tempRoomID.Length > 0)
                        {
                            strSel = tempRoomID;
                        }
                        else
                        {
                            //[规则]当超声房间全部关闭时,自动选择超声科
                            strSel = "[超声科]";
                        }
                    }
                    else if ("[324][325][326]".IndexOf(first_room_id) >= 0)
                    {
                        String tempRoomID = get_active_RoomID_Cluster("[324][325][326]");
                        if (tempRoomID.Length > 0)
                        {
                            strSel = tempRoomID;
                        }
                    }

                    roomList.Add(strSel);
                }
                catch
                {
                    //如果还出错,对此Exam不登记
                    roomList.Add(VALID_ROOM_ID);
                }

            }//end for

            return roomList;
        }


        /// <summary>
        /// 对同一个EXAM的多个房间,选择返回一个(活动房间)(未完成检查数最少的房间)
        /// </summary>
        /// <param name="listRound_2"></param>
        /// <returns></returns>
        private List<String> parse_4_Round_Recommanded_Room_Handle(List<String> listRound_2)
        {
            //[RoomID]:register_table.CheckRoom

            //逐一处理每个检查的房间串,并处理返回结果
            List<String> roomList = new List<string>();

            int nCount = register_table.Rows.Count;
            for (int i = 0; i < nCount; i++)
            {
                //注: 由于Round_1_2保证了数据的完整性,在Round3无需进行CSTR.isEmpty的处理了
                String rooms_id_origin = "";
                String rooms_id_round_2 = "";

                try
                {
                    rooms_id_origin = CSTR.ObjectTrim(register_table.Rows[i]["CheckRoom"]);
                    rooms_id_round_2 = listRound_2[i];

                    //如果只有一个房间,直接写回原串
                    String[] rooms_arr_round_2 = CSTR.splitRooms(rooms_id_round_2);
                    if (rooms_arr_round_2.Length <= 1)
                    {
                        roomList.Add(rooms_id_round_2);
                        continue;
                    }

                    //[规则] 同一EXAM的多个房间,需要根据排队的未完成检查数来选择其中一个数目较少的
                    int min_count = 9999;
                    String min_room_id = rooms_arr_round_2[0];
                    foreach (String roomID in rooms_arr_round_2)
                    {
                        if (CSTR.isEmpty(roomID)) continue;

                        int queue_count = DatabaseCache.Queue_Length_Count_Specified_Room_Ignore_QueueActive(roomID);
                        if (queue_count < min_count)
                        {
                            //更新min_count到本房间
                            min_count = queue_count;
                            min_room_id = roomID;
                        }
                    }
                    //如果选择失败,则不予登记
                    if (CSTR.isEmpty(min_room_id))
                    {
                        min_room_id = VALID_ROOM_ID;
                    }

                    roomList.Add(min_room_id);
                }
                catch
                {
                    //如果还出错,直接写回Round1的串
                    String strSel = rooms_id_round_2;
                    if (CSTR.isEmpty(rooms_id_round_2))
                    {
                        strSel = rooms_id_origin;
                    }

                    //如果多个房间,简单选择第一个
                    String[] room_arr = CSTR.splitRooms(strSel);
                    if (null == room_arr || room_arr.Length <= 0)
                    {
                        strSel = VALID_ROOM_ID;
                    }
                    else
                    {
                        strSel = room_arr[0];
                    }

                    roomList.Add(strSel);
                }

            }//end for

            return roomList;
        }

        /// <summary>
        /// 同类型房间重复出现时,只选择唯一的一个,避免重复
        /// </summary>
        /// <param name="listRound_3"></param>
        /// <returns></returns>
        private List<String> parse_5_Round_RoomType_Uniqueness_Handle(List<String> listRound_3)
        {
            //[RoomID]:register_table.CheckRoom

            //注: 不生成新的返回List,而是对listRound_3进行修改后返回

            //保证listRound_3中的每一项都不为空,而且只有一个房间
            for (int i = 0; i < listRound_3.Count; i++)
            {
                String roomID = listRound_3[i];
                String[] room_arr = CSTR.splitRooms(roomID);

                //确保不为空
                if (CSTR.isEmpty(roomID) || CSTR.splitRoomsCount(room_arr) <= 0)
                {
                    listRound_3[i] = VALID_ROOM_ID;
                    continue;
                }

                //确保只有一个房间
                if (room_arr.Length > 1)
                {
                    //超过一个房间,则取第一个
                    listRound_3[i] = room_arr[0];
                }
            }


            //同类房间如果有重复,需要从中选择唯一的一个房间
            List<String> listSameType_RoomCluster = new List<string>();
            listSameType_RoomCluster.Add("[302][303][304][305][405]");
            listSameType_RoomCluster.Add("[306][307][401][402][403][404]");
            listSameType_RoomCluster.Add("[309][311]");
            listSameType_RoomCluster.Add("[308][312][313]");
            listSameType_RoomCluster.Add("[324][325][326]");
            listSameType_RoomCluster.Add("[322][406]");
            //保存listRound_3中的同类房间
            List<String> listExistRooms = new List<string>();
            for (int i = 0; i < listSameType_RoomCluster.Count; i++)
            {
                listExistRooms.Add("");
            }

            //获取listRound_3中的同类房间串
            foreach (String roomID in listRound_3)
            {
                for (int i = 0; i < listSameType_RoomCluster.Count; i++)
                {
                    String room_cluster_full = listSameType_RoomCluster[i];
                    if (room_cluster_full.IndexOf(roomID) >= 0)
                    {
                        //读取目前保存的同类房间串
                        String room_exist = listExistRooms[i];
                        //先清除,再添加,不会重复出现同一个RoomID
                        room_exist = room_exist.Replace(roomID, "");
                        room_exist += roomID;
                        //重新保存
                        listExistRooms[i] = room_exist;
                    }
                }//end for
            }//end foreach

            //判断是否重复
            for (int i = 0; i < listExistRooms.Count; i++)
            {
                String rooms_id = listExistRooms[i];
                String[] room_arr = CSTR.splitRooms(rooms_id);
                if (CSTR.splitRoomsCount(room_arr) <= 1) continue;

                //两个或以上同类房间,出现重复
                String strSel = "";
                //获取未关闭的房间
                String strActive = get_active_RoomID_Cluster(rooms_id);
                String[] arr_active = CSTR.splitRooms(strActive);
                if (CSTR.isEmpty(strActive) || CSTR.splitRoomsCount(arr_active) <= 0)
                {
                    //如果重复房间都关闭,则直接选择第一个
                    strSel = room_arr[0];
                }
                else
                {
                    //选择已开启房间的第一个
                    strSel = arr_active[0];
                }

                //更新到listRound_3
                for (int i_round_3 = 0; i_round_3 < listRound_3.Count; i_round_3++)
                {
                    String room_round_3 = listRound_3[i_round_3];
                    if (rooms_id.IndexOf(room_round_3) >= 0)
                    {
                        if (false == CSTR.isEmpty(strSel))
                        {
                            //更新,去除多个重复同类房间,修改为唯一的一个
                            listRound_3[i_round_3] = strSel;
                        }
                    }
                }

            }//end for

            return listRound_3;

        }


        #endregion <END>这是手环配发时调用的方法,用于综合分析所有的项目,归类合并房间,选择一个最优房间




        #region <BEGIN>基本处理支持函数-----------------------------------------------------------
        //-------------合并register_table房间成为一个串---------------------------------
        private String get_all_RoomID_cluster()
        {
            //[RoomID]:register_table.CheckRoom
            if (CSTR.IsTableEmpty(register_table)) return null;

            String str = "";
            foreach (DataRow row in register_table.Rows)
            {
                str += CSTR.ObjectTrim(row["CheckRoom"]);
            }

            return str;
        }

        //-------------把每条记录的房间号按行顺序提取到List<>中---------------------------------
        private List<String> get_RoomID_List()
        {
            //[RoomID]:register_table.CheckRoom
            if (CSTR.IsTableEmpty(register_table)) return null;

            List<String> listRoomID = new List<string>();
            foreach (DataRow row in register_table.Rows)
            {
                listRoomID.Add(CSTR.ObjectTrim(row["CheckRoom"]));
            }

            return listRoomID;
        }

        //-------------从指定的房间串中返回active的房间过滤----------------------------------------
        private String get_active_RoomID_Cluster(String roomCluster)
        {
            if (CSTR.isEmpty(roomCluster)) return "";
            String[] room_arr = CSTR.splitRooms(roomCluster);
            if (null == room_arr || room_arr.Length <= 0) return "";

            String retCluster = "";
            foreach (String roomID in room_arr)
            {
                bool is_active = DatabaseCache.RoomInfo_is_active(roomID);
                if (is_active)
                {
                    retCluster += roomID;
                }
            }

            return retCluster;
        }
        #endregion <END>基本处理支持函数-----------------------------------------------------------




        #region <BEGIN>-----------------------RLUES<(Study)规则汇总>------------------------------------
        //----------------查找STUDY有无原始的[312][313]房间-------------------------------------------
        private bool StudyHas_312_313_Room()
        {
            //[RoomID]:register_table.CheckRoom
            if (CSTR.IsTableEmpty(register_table)) return false;

            //[规则]只要任意Exam的房间串中包含[312]或[313],就是拥有职检检查

            bool bRet = false;
            foreach (DataRow row in register_table.Rows)
            {
                String roomID = CSTR.ObjectTrim(row["CheckRoom"]);
                if (roomID.IndexOf("[312]") >= 0 ||
                    roomID.IndexOf("[313]") >= 0 ||
                    roomID.IndexOf("[308]") >= 0)
                {
                    bRet = true;
                }
            }

            return bRet;
        }

        //----------------查找STUDY有无原始的[310]房间-------------------------------------------
        private bool StudyHas_310_alone()
        {
            //[RoomID]:register_table.CheckRoom
            if (CSTR.IsTableEmpty(register_table)) return false;

            //[规则]只要任意Exam的房间是单独的[310]或[311],就是独立到[311],而不能到[309]
            //[规则]职检的检查在有[310]时同样执行此规则

            bool bRet = false;
            foreach (DataRow row in register_table.Rows)
            {
                String roomID = CSTR.ObjectTrim(row["CheckRoom"]);
                if (roomID.Equals("[310]"))
                {
                    bRet = true;
                }
            }

            return bRet;
        }

        //----------------查找STUDY有无原始的[311]房间-------------------------------------------
        private bool StudyHas_311_alone()
        {
            //[RoomID]:register_table.CheckRoom
            if (CSTR.IsTableEmpty(register_table)) return false;

            //[规则]只要任意Exam的房间是单独的[310]或[311],就是独立到[311],而不能到[309]
            //[规则]职检的检查在有[310]时同样执行此规则

            bool bRet = false;
            foreach (DataRow row in register_table.Rows)
            {
                String roomID = CSTR.ObjectTrim(row["CheckRoom"]);
                if (roomID.Equals("[311]"))
                {
                    bRet = true;
                }
            }

            return bRet;
        }

        //----------------判断是否为职检---------------------------------------------------------
        private bool StudyIsVocationType()
        {
            //[RoomID]:register_table.CheckRoom
            if (CSTR.IsTableEmpty(register_table)) return false;

            //[规则]TFD体检项目:只要任意一个Exam的ExamType属于职检,
            //[规则]SpecialExam: 检查名称中包含职检字样,即为职检检查
            //[规则]TFD体检项目与SpecialExam项目的区分:register_table.DWMC='非天方达'

            bool is_vocation_exam = false;
            foreach (DataRow row in register_table.Rows)
            {
                String DWMC = CSTR.ObjectTrim(row["DWMC"]);
                String ProcedureStepName = CSTR.ObjectTrim(row["TJXMMC"]);

                String TJLB = CSTR.ObjectTrim(row["TJLB"]);
                int n_TJLB = CSTR.convertToInt(TJLB);//转换为int_体检类别

                if (DWMC.Equals("非天方达"))
                {
                    //SpecialExam检查项目
                    if (ProcedureStepName.IndexOf("职检") >= 0)
                    {
                        is_vocation_exam = true;
                    }
                }
                else
                {
                    //TFD检查项目
                    if (n_TJLB != -1 && n_TJLB >= 7 && n_TJLB <= 15)
                    {
                        is_vocation_exam = true;
                    }
                }

            }//end foreach

            return is_vocation_exam;
        }

        #endregion <END>-----------------------RLUES<(Study)规则汇总>------------------------------------

        //登记成功,返回NEW_DJLSH,登记失败,返回null;
        public String register()
        {
            String str_ret_DJLSH = null;

            if (CSTR.IsTableEmpty(register_table)) return null;
            if (CSTR.isEmpty(rfid_no)) return null;
            if (CSTR.isEmpty(ring_no)) return null;

            ExamQueue queue = new ExamQueue();

            try
            {
                //生成每个检查的检查房间-------------------------------------------------------------------------
                List<String> reg_list_RoomID = parse_and_allot_RoomID_for_STUDY();
                if (null == reg_list_RoomID) return null;
                if (reg_list_RoomID.Count <= 0) return null;
                //生成每个检查的检查房间-------------------------------------------------------------------------

                //通用数据以第一条记录为准------------------------------------------------------------------------
                DataRow rowFirst = register_table.Rows[0];
                //PatientInfo级别的信息(同一使用第一行的数据)
                String PatientNameChinese = CSTR.ObjectTrim(rowFirst["XM"]);
                String PatientGender = CSTR.ObjectTrim(rowFirst["XB"]);
                if (PatientGender.Equals("0")) PatientGender = "女"; else PatientGender = "男";//0:女 1:男 %:未知
                String SFZH = CSTR.ObjectTrim(rowFirst["SFZH"]);//身份证号
                String Phone = (CSTR.ObjectTrim(rowFirst["p_Phone"]).Length >//两个电话取长度长的那个
                                CSTR.ObjectTrim(rowFirst["d_Phone"]).Length) ?
                                CSTR.ObjectTrim(rowFirst["p_Phone"]) : CSTR.ObjectTrim(rowFirst["d_Phone"]);
                String PatientIntraID = CSTR.ObjectTrim(rowFirst["TJBH"]);
                String DJLSH = CSTR.ObjectTrim(rowFirst["DJLSH"]);
                String TJLB = CSTR.ObjectTrim(rowFirst["TJLB"]);
                String TJLBMC = CSTR.ObjectTrim(rowFirst["TJLBMC"]);
                String DWBH = CSTR.ObjectTrim(rowFirst["DWBH"]);
                String DWMC = CSTR.ObjectTrim(rowFirst["DWMC"]);
                String ExamAccessionID = this.ring_no;//用于显示序号
                //获取激活时间
                DateTime arrivalTime = DateTime.Now;
                String strArrivalTime = arrivalTime.ToString("HH:mm:ss");//只取得时间部分,用以生成QueueID
                String strArrivalDateTime = arrivalTime.ToString("yyyyMMdd HH:mm:ss");//取得日期及时间(格式化时间)
                //生成QueueID
                String QueueID = String.Format("time_to_sec('{0}')", strArrivalTime);
                String ExamArrivalDateTime = "NOW()";
                //生成检查来源(类别)
                String PreExamFrom = "个检";//体检来源(类别)
                if (STUDY_is_vacation_exam) PreExamFrom = "职检";
                //PatientInfo级别的信息同一使用第一行的数据为准----------------------------------------------------

                //生成每一个Exam的JiaohaoTable和JiaohaoExamInfo的Insert的Sql语句
                List<String> reg_list_sql_JiaohaoTable = new List<string>();
                List<String> reg_list_sql_JiaohaoExamInfo = new List<string>();
                String reg_str_RoomID_registed_cluster = "";//记录已经登记了的RoomID列表
                for (int i = 0; i < register_table.Rows.Count; i++)
                {
                    //取得行数据
                    DataRow row = register_table.Rows[i];

                    //ExamInfo级别的数据,每个Row会有不同---------------------------------
                    String ModalityID = CSTR.ObjectTrim(row["CheckRoom"]);
                    String OrderProcedureName = CSTR.ObjectTrim(row["ExamTypeMC"]);
                    String ProcedureStepID = CSTR.ObjectTrim(row["TJXMBH"]);
                    String ProcedureStepName = CSTR.ObjectTrim(row["TJXMMC"]);
                    String ExamType = CSTR.ObjectTrim(row["ExamType"]);
                    String ReorderReason = "";// CSTR.ObjectTrim(row["TSXX"]);//提示信息
                    //ExamInfo级别的数据,每个Row会有不同---------------------------------

                    //获取RoomID
                    String RoomID = reg_list_RoomID[i];
                    if (CSTR.isEmpty(RoomID) || RoomID.Equals(VALID_ROOM_ID))
                    {
                        //如果是无效房间,则生成空的SQL语句
                        reg_list_sql_JiaohaoExamInfo.Add("");
                        reg_list_sql_JiaohaoTable.Add("");

                        //处理下一个
                        continue;
                    }

                    //有了唯一的RoomID后,定义OrderID,用于同一房间检查的详细信息查询
                    String OrderID = CSTR.trim(DJLSH + RoomID);
                    //ExamID也要写入到JiaohaoExamInfo,用于和IndexID的对应,需要保证每个Exam不同
                    String ExamID = String.Format("{0}.{1}.{2}", OrderID, ProcedureStepID, i.ToString());

                    //合并相同房间的RoomID字段和检查方法字段,保存到JiaohaoTable.ArrayRoomID和JiaohaoTable.ArrayProcedureStepName
                    String ArrayRoomID = "";
                    String ArrayProcedureStepName = "";
                    for (int reg_point = 0; reg_point < reg_list_RoomID.Count; reg_point++)
                    {
                        String reg_room_id = reg_list_RoomID[reg_point];
                        if (RoomID.Equals(reg_room_id))
                        {
                            String roomid_for_arr = CSTR.ObjectTrim(register_table.Rows[reg_point]["CheckRoom"]);
                            String procedureName_for_arr = CSTR.ObjectTrim(register_table.Rows[reg_point]["TJXMMC"]);
                            //去掉敏感字符';'
                            roomid_for_arr = roomid_for_arr.Replace(";", "");
                            procedureName_for_arr = procedureName_for_arr.Replace(";", "");
                            //加入到Array字段
                            if (ArrayRoomID.Length > 0) ArrayRoomID += ";";
                            ArrayRoomID += roomid_for_arr;
                            if (ArrayProcedureStepName.Length > 0) ArrayProcedureStepName += ";";
                            ArrayProcedureStepName += procedureName_for_arr;
                        }
                    }

                    //检查状态的确定
                    String ExamSatus = "2";//默认为未检
                    String status = "0";//默认为未检
                    String IsOver = "0";//默认为未检
                    String IsNeedQueue = "1";//默认需要排队
                    String IsNeedVoice = "1";//默认需要TTS语音

                    //对roomID,查询房间的三个状态 active/needQueue/needVoice
                    bool bActive = DatabaseCache.RoomInfo_is_active(RoomID);
                    bool bNeedQueue = DatabaseCache.RoomInfo_is_need_queue(RoomID);
                    bool bNeedVoice = DatabaseCache.RoomInfo_is_need_voice(RoomID);

                    //只有NeedQueue为false才无需排队,不论房间是否active或disactive
                    if (bNeedQueue == false)
                    {
                        ExamSatus = "3";
                        status = "1";
                        IsOver = "1";
                        IsNeedQueue = "0";
                        IsNeedVoice = "0";
                    }
                    else
                    {
                        ExamSatus = "2";
                        status = "0";
                        IsOver = "0";
                        IsNeedQueue = "1";
                        if (bNeedVoice)
                        {
                            IsNeedVoice = "1";
                        }
                        else
                        {
                            IsNeedVoice = "0";
                        }
                    }

                    //生成插入到JiaohaoTable的语句
                    String sql_JiaohaoTable = String.Format("insert into JiaohaoTable " +

                        " (QueueID,PatientNameChinese,PatientGender," +//1
                        "SFZH,Phone,PatientIntraID," +

                        "DJLSH,Rfid,RfidName," +//2
                        "TJLB,TJLBMC,DWBH," +

                        "DWMC,ExamID,OrderID," +//3
                        "ExamAccessionID,ExamArrivalDateTime,ModalityID," +

                        "OrderProcedureName,ProcedureStepID,ProcedureStepName," +//4
                        "ExamType,RoomID,ReorderReason," +

                        "PreExamFrom,ExamSatus,status," +//5
                        "IsOver,IsNeedQueue,IsNeedVoice," +
                        "ArrayRoomID,ArrayProcedureStepName) VALUES " +

                        " ({0},'{1}','{2}'," +//1
                        " '{3}','{4}','{5}'," +

                        " '{6}','{7}','{8}'," +//2
                        " '{9}','{10}','{11}'," +

                        " '{12}','{13}','{14}'," +//3
                        " '{15}',{16},'{17}'," +

                        " '{18}','{19}','{20}'," +//4
                        " '{21}','{22}','{23}'," +

                        "'{24}',{25},{26}," +//5
                        "{27},{28},{29}," +
                        "'{30}','{31}')",

                        QueueID, PatientNameChinese, PatientGender,//1
                        SFZH, Phone, PatientIntraID,

                        DJLSH, this.rfid_no, this.ring_no,//2
                        TJLB, TJLBMC, DWBH,

                        DWMC, ExamID, OrderID,//3
                        ExamAccessionID, ExamArrivalDateTime, ModalityID,

                        OrderProcedureName, ProcedureStepID, ProcedureStepName,//4
                        ExamType, RoomID, ReorderReason,

                        PreExamFrom, ExamSatus, status,//5
                        IsOver, IsNeedQueue, IsNeedVoice,
                        ArrayRoomID, ArrayProcedureStepName
                        );

                    //生成插入到JiaohaoExamInfo的语句
                    String sql_JiaohaoExamInfo = String.Format("insert into JiaohaoExamInfo " +
                        " (ExamID,OrderID,RoomID,CheckRoom," +
                        "ProcedureStepID,ProcedureStepName,ExamType,RfidName) VALUES " +
                        " ('{0}','{1}','{2}','{3}'," +
                        "'{4}','{5}','{6}','{7}')",
                        ExamID, OrderID, RoomID, ModalityID,
                        ProcedureStepID, ProcedureStepName, ExamType, this.ring_no);

                    //对于相同的RoomID,在JiaohaoTable中只能写入一次
                    if (reg_str_RoomID_registed_cluster.IndexOf(RoomID) >= 0)
                    {
                        //如果此Exam的RoomID已经登记了,则不再写入相同的JiaohaoTable记录
                        sql_JiaohaoTable = "";
                    }
                    else
                    {
                        //记录本次的RoomID到已完成房间串
                        reg_str_RoomID_registed_cluster += RoomID;
                    }
                    //保存SQL语句
                    reg_list_sql_JiaohaoTable.Add(sql_JiaohaoTable);
                    reg_list_sql_JiaohaoExamInfo.Add(sql_JiaohaoExamInfo);
                }//end for (int i = 0; i < register_table.Rows.Count; i++)

                //重置返回值
                str_ret_DJLSH = null;

                //逐条写入数据库
                int nCount = queue.getDB().update_use_Transaction(reg_list_sql_JiaohaoTable);
                if (nCount > 0)
                {
                    //不再插入检查到JiaohaoExamInfo
                    //int nCount2 = queue.getDB().update_use_Transaction(reg_list_sql_JiaohaoExamInfo);

                    str_ret_DJLSH = DJLSH;//设置返回值
                }    
            }
            catch (Exception exp)
            {
                str_ret_DJLSH = null;
            }

            return str_ret_DJLSH;
        }

        #region 体检系统登记查询-----------------------------------------------------------<Start>
        public DataTable TFD_Register_Query_Stusy()
        {
            if (CSTR.isEmpty(tfd_DJLSH)) return null;


            String sql = String.Format(@"select 
patient.TJBH,
patient.XM,
patient.XB,
patient.SFZH,
patient.PHONE as p_PHONE,
djb.PHONE as d_PHONE,
djb.DJLSH,
djb.DWBH,
(select top 1 MC from futian_user.HYDWDMB where BH=DWBH) as DWMC,
djb.TJRQ,
jlb.JCRQ as jlb_JCRQ,
djb.TJLB,
(select top 1 MC from futian_user.TJ_TJLB where BH=djb.TJLB) as TJLBMC,
djb.RYLB,
(select top 1 MC from futian_user.TJ_TJRYLB where BH=djb.RYLB) as RYLBMC,
jlb.XH,
jlb.LXBH as ExamType,
(select top 1 MC from futian_user.TJ_TJLXB where LXBH=jlb.LXBH) as ExamTypeMC,
jlb.TJXMBH,
(select top 1 MC from futian_user.TJ_ZHXM_HD where BH=jlb.TJXMBH) as TJXMMC,
(select top 1 TSXX from futian_user.TJ_ZHXM_HD where BH=jlb.TJXMBH) as TSXX,
(select top 1 CheckRoom from futian_user.TJ_ZHXM_HD where BH=jlb.TJXMBH) as CheckRoom,
jlb.ISOVER
from futian_user.RYXX patient,futian_user.TJ_TJDJB djb,futian_user.TJ_TJJLB jlb
where 
djb.TJBH=patient.TJBH
and djb.TJBH=jlb.TJBH
and djb.TJCS=jlb.TJCS
and djb.DJLSH='{0}'", tfd_DJLSH);
            return new ExamQueue().getTijianDB().query(sql);
        }
        public DataTable TFD_Register_Query_Stusy_BAK()
        {
            //此查询没有考虑TJCS,会导致TJCS>1的检查项目变多
            if (CSTR.isEmpty(tfd_DJLSH)) return null;


            String sql = String.Format(@"select 
patient.TJBH,
patient.XM,
patient.XB,
patient.SFZH,
patient.PHONE as p_PHONE,
djb.PHONE as d_PHONE,
djb.DJLSH,
djb.DWBH,
(select top 1 MC from futian_user.HYDWDMB where BH=DWBH) as DWMC,
djb.TJRQ,
jlb.JCRQ as jlb_JCRQ,
djb.TJLB,
(select top 1 MC from futian_user.TJ_TJLB where BH=TJLB) as TJLBMC,
djb.RYLB,
(select top 1 MC from futian_user.TJ_TJRYLB where BH=RYLB) as RYLBMC,
jlb.XH,
jlb.LXBH as ExamType,
(select top 1 MC from futian_user.TJ_TJLXB where LXBH=jlb.LXBH) as ExamTypeMC,
jlb.TJXMBH,
(select top 1 MC from futian_user.TJ_ZHXM_HD where BH=TJXMBH) as TJXMMC,
(select top 1 TSXX from futian_user.TJ_ZHXM_HD where BH=TJXMBH) as TSXX,
(select top 1 CheckRoom from futian_user.TJ_ZHXM_HD where BH=TJXMBH) as CheckRoom,
jlb.ISOVER
from futian_user.RYXX patient,futian_user.TJ_TJDJB djb,futian_user.TJ_TJJLB jlb
where 
djb.TJBH=patient.TJBH
and djb.TJBH=jlb.TJBH
and djb.DJLSH='{0}'
order by jlb.XH", tfd_DJLSH);
            return new ExamQueue().getTijianDB().query(sql);
        }

        public DataTable SpecialExam_Register_Query_Stusy(String id_cluster)
        {
            //只能用于天方达登记的附加项目查询
            if (CSTR.isEmpty(id_cluster)) return null;

            String sql = String.Format(@"select
CONCAT('{0}',DATE_FORMAT(NOW(),'%H%i')) as TJBH,
'{3}' as XM,
'{4}' as XB,
'' as SFZH,
'' as p_PHONE,
'' as d_PHONE,
CONCAT('{0}',DATE_FORMAT(NOW(),'%H%i')) as DJLSH,
'0000' as DWBH,
'非天方达' as DWMC,
now() as TJRQ,
now() as jlb_JCRQ,
TJLB,
TJLBMC,
'01' as RYLB,
'SA' as RYLBMC,
CONCAT(DATE_FORMAT(NOW(),'%H%i'),ProcedureStepID) XH,
ExamType,
OrderProcedureName as ExamTypeMC,
CONCAT(TJLB,ProcedureStepID) as TJXMBH,
ProcedureStepName as TJXMMC,
ExamTip as TSXX,
RoomID as CheckRoom,
'0' as ISOVER
from JiaohaoSpecialExam
where -- TJLBMC='{1}' and
 id in ({2})", this.rfid_no, "附加项目", id_cluster, "p_name", "男");

            return new ExamQueue().getDB().query(sql);
        }

        #endregion 体检系统登记查询-----------------------------------------------------------<End>

    }//end class
}//end namespace

