using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//wright 
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Linq;
using System.Management;

namespace nrfGoReplacer
{

    public enum CMD_TYPE
    {
        RECOVER,
        RECOVER_MULTI,
        ERASE_ALL,
        ERASE_MULTI_ALL,
        SYS_RESET,
        SYS_MULTI_RESET,
        PROGRAM_SD,
        PROGRAM_APP,
        PROGRAM_MULTI_APP,
        BT_ADDRESS_READ,
        UICR_VALUE_READ,
        UICR_VALUE_SET,
        RBP,
        RBP_MULTI,
        MERGE_TEMP,
        MERGE,
        READCODE,
        READ_IC_VERSION,
        READ_ALL_SERIAL_NO
    }

    

    public partial class Form1 : Form
    {
        public string str_vernum = "3.1.0";
        public string str_cmd_exe_path = $"cmd.exe";
        public string str_IC_number = string.Empty;
        public string[] str_arr_serialsNOs = { };
        public Dictionary<int, ComboBox> comboxDic;
        public int int_combox_num = 10;
        public int int_combox_default_index = -1;
        public string str_default_output_folder_path = AppDomain.CurrentDomain.BaseDirectory;
        public string str_temp_merge_outputFile_fullName = $"{AppDomain.CurrentDomain.BaseDirectory}\\Merge_Temp.hex";

        delegate void Form_enable_delegation();
        delegate void Form_disable_delegation();

         void Disable_Form()
        { this.Enabled = false; }

         void Enable_Form()
        { this.Enabled = true; }

        public Form1()
        {
            InitializeComponent();
            //show version num
            version_num.Text = str_vernum;

            //fix form size 
            this.MinimumSize = new Size(550, int.MaxValue);
            this.MaximumSize = new Size(550, int.MaxValue);
            this.MaximizeBox = false;

            //read all connected jlink serial numbers  
            ReadAllSerialNO();
            ComboxsItemSettingsFormLoad();

            //Make different thread could control form
            Control.CheckForIllegalCrossThreadCalls = false;

            //bring default path to textboxes (Readcode/Mergehex output path)
            textBox15.Text = str_default_output_folder_path;
            textBox18.Text = str_default_output_folder_path;

            //For Detecting USB Plug/Unplug Event (Dynamic change MultiTarget List)
            USB_Event_Watcher();
        }




        #region Programer

        #region Single Target

        private void Recover_Click(object sender, EventArgs e)
        {
            ReadIcVersion();
            string str_cmd = $"nrfjprog.exe {str_IC_number} --recover";
            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.RECOVER);
        }

        private void EraseAll_Click(object sender, EventArgs e)
        {
            ReadIcVersion();
            string str_cmd = $"nrfjprog.exe {str_IC_number} -e";
            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.ERASE_ALL);
        }

        private void Reset_Click(object sender, EventArgs e)
        {
            ReadIcVersion();
            string str_cmd = $"nrfjprog.exe {str_IC_number} -r";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.SYS_RESET);
        }

        private void SoftDevicePath_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox1.Text = ofd.FileName;
            }
        }

        private void SoftdeviceProgram_Click(object sender, EventArgs e)
        {
            ReadIcVersion();
            string sd_filePath = textBox1.Text;
            string str_cmd = $"nrfjprog.exe {str_IC_number} --program {sd_filePath} --verify";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.PROGRAM_SD);
        }

        private void ApplicationPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox2.Text = ofd.FileName;
            }
        }

        private void ApplicationProgram_Click(object sender, EventArgs e)
        {
            ReadIcVersion();
            string sd_filePath = textBox2.Text;
            string str_cmd = $"nrfjprog.exe {str_IC_number} --program {sd_filePath} --verify";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.PROGRAM_APP);

            if (checkBox2.Checked)
            {
                str_cmd = $"nrfjprog.exe {str_IC_number}  --rbp ALL";
                OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.RBP);

            }
                
        }

        #endregion


        #region Multi Target

        enum MULTI_TARGET_ACTION_TYPE
        {
            RECOVER,
            RESET,
            ERASEALL,
            PROGRAM
        }

        public int LastActionItemIndex;

        private void CheckLastActionItem()
        {
            LastActionItemIndex = 0;
            for (int i = 1; i <=int_combox_num; i++)
            {
                if (comboxDic[i].SelectedIndex != int_combox_default_index)
                    LastActionItemIndex = i;

            }
           
        }

        private void MultiTarget_FileBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox6.Text = ofd.FileName;
            }
        }

        // MultiProgram Click
        private void MultiTarget_Program_Click(object sender, EventArgs e)
        {
            MultiTarget_Actions_Process(MULTI_TARGET_ACTION_TYPE.PROGRAM);
        }

        private void MultiTarget_EraseAll_Click(object sender, EventArgs e)
        {
            MultiTarget_Actions_Process(MULTI_TARGET_ACTION_TYPE.ERASEALL);
        }

        private void MultiTarget_Reset_Click(object sender, EventArgs e)
        {
            MultiTarget_Actions_Process(MULTI_TARGET_ACTION_TYPE.RESET);
        }

        private void MultiTarget_Recover_click(object sender, EventArgs e)
        {
            MultiTarget_Actions_Process(MULTI_TARGET_ACTION_TYPE.RECOVER);
        }

        private void MultiTarget_Actions_Process(MULTI_TARGET_ACTION_TYPE type)
        {
            //check IC version
            string str_filepath = textBox6.Text;
            string str_serial_no = str_arr_serialsNOs[0];
            ReadIcVersion(str_serial_no);
            MultiThreadsAction mtc;
            Thread thread;

            //For Showing last log while process multi thread action
            bool IsLastControlItem = false;
            //For controling form (enable or disable)
            bool IsFormControl = false;

            //check last action item (for form control)
            CheckLastActionItem();

            //For Comboxes being selected , perform programing action.
            //1.In Multithread, only the last action will perform form controlling(enable/disable)
            //2.In Multithread, we display the result only at the finish of last action.
            for (int i = 1; i <= int_combox_num; i++)
            {
                if (comboxDic[i].SelectedItem !=null && 
                    (Int32)comboxDic[i].SelectedIndex != 0)
                {
                    IsLastControlItem = (LastActionItemIndex == i) ? true : false;
                    IsFormControl = (LastActionItemIndex == i) ? true : false;

                    switch (type)
                    {
                        case MULTI_TARGET_ACTION_TYPE.RESET:

                            mtc =
                               new MultiThreadsAction(this,str_IC_number,
                               comboxDic[i].SelectedItem.ToString(), str_filepath,
                               IsLastControlItem, IsLastControlItem);
                            thread = new Thread(mtc.Reset_ic);
                            thread.Start();

                            break;
                        case MULTI_TARGET_ACTION_TYPE.ERASEALL:

                            mtc =
                                new MultiThreadsAction(this,str_IC_number,
                                comboxDic[i].SelectedItem.ToString(), str_filepath,
                                IsFormControl, IsLastControlItem);
                            thread = new Thread(mtc.Erase_all);
                            thread.Start();

                            break;
                        case MULTI_TARGET_ACTION_TYPE.PROGRAM:

                            if (checkBox1.Checked){
                                mtc =
                                  new MultiThreadsAction(this,str_IC_number,
                                  comboxDic[i].SelectedItem.ToString(), str_filepath,
                                  IsFormControl, IsLastControlItem);
                                thread = new Thread(mtc.RBP);
                                thread.Start();
                            }
                            else {
                                mtc =
                                new MultiThreadsAction(this,str_IC_number,
                                    comboxDic[i].SelectedItem.ToString(), str_filepath,
                                    IsFormControl, IsLastControlItem);
                                thread = new Thread(mtc.Program_Hex);
                                thread.Start();
                            }
       
                            

                            break;

                        case MULTI_TARGET_ACTION_TYPE.RECOVER:

                            mtc =
                                new MultiThreadsAction(this,str_IC_number,
                                comboxDic[i].SelectedItem.ToString(), str_filepath,
                                IsFormControl, IsLastControlItem);
                            thread = new Thread(mtc.Recover);
                            thread.Start();

                            break;
                        default:
                            break;
                    }

                }
            }

        }

        #endregion

        #endregion

        #region Gerneral

        //UICR & BT Address
        private void UICR_READ_Click(object sender, EventArgs e)
        {
            string str_address = $"0x{textBox4.Text}";

            string str_cmd = $"nrfjprog.exe {str_IC_number} --memrd {str_address} --n 4";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.UICR_VALUE_READ);
        }

        private void UICR_VALUE_SET_Click(object sender, EventArgs e)
        {
            string str_address = $"0x{textBox4.Text}";
            string str_value = $"0x{textBox3.Text}";

            string str_cmd = $"nrfjprog.exe {str_IC_number} --memwr {str_address} --val {str_value} --verify";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.UICR_VALUE_SET);
        }

        private void BT_ADDRESS_READ_Click(object sender, EventArgs e)
        {
            string str_address = $"0x100000A0";

            string str_cmd = $"nrfjprog.exe {str_IC_number} --memrd {str_address} --n 16";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.BT_ADDRESS_READ);
        }

        //Merge
        private void Merge_FilePath1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox12.Text = ofd.FileName;
            }
        }

        private void Merge_FilePath2_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox13.Text = ofd.FileName;
            }
        }

        private void Merge_FilePath3_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox14.Text = ofd.FileName;
            }
        }

        private void Merge_FilePath4_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox7.Text = ofd.FileName;
            }
        }

        private void Merge_OutputFilePath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "Select output path of  merge hexfile "; //not mandatory

            if (fbd.ShowDialog() == DialogResult.OK)
                textBox15.Text = fbd.SelectedPath;
            else
                textBox15.Text = string.Empty;

        }

        private void Merge_Click(object sender, EventArgs e)
        {
            //wright : 0414 fix path contain empty bug
            string str_path_empty_fix = "\"";
            string str_mergeFile1 = str_path_empty_fix + textBox12.Text + str_path_empty_fix;
            string str_mergeFile2 = str_path_empty_fix + textBox13.Text + str_path_empty_fix;
            string str_mergeFile3 = str_path_empty_fix + textBox14.Text + str_path_empty_fix;
            string str_mergeFile4 = str_path_empty_fix + textBox7.Text + str_path_empty_fix;

            string str_outputFile_fullName = $"{textBox15.Text}\\{textBox16.Text}";
            

            //MergeHex.exe could only merge 3 hex files at once
            if (!String.IsNullOrEmpty(str_mergeFile1)
                && !String.IsNullOrEmpty(str_mergeFile2)
                && !String.IsNullOrEmpty(str_mergeFile3)
                && !String.IsNullOrEmpty(str_mergeFile4))
            {
                string str_cmd = $"mergehex.exe -m {str_mergeFile1} {str_mergeFile2} {str_mergeFile3} -o {str_temp_merge_outputFile_fullName}";
                string str_cmd_exe_path = $"cmd.exe";
                OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.MERGE_TEMP);

                str_cmd = $"mergehex.exe -m {str_temp_merge_outputFile_fullName} {str_mergeFile4}  -o {str_outputFile_fullName}";
                OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.MERGE);
            }
            else
            {
                string str_cmd = $"mergehex.exe -m {str_mergeFile1} {str_mergeFile2} {str_mergeFile3} {str_mergeFile4} -o {str_outputFile_fullName}";
                string str_cmd_exe_path = $"cmd.exe";

                OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.MERGE);
            }

            
        }

        //Readcode && Recovery

        private void ReadCode_OutputFilePath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "Select output path of Readcode hexfile "; //not mandatory

            if (fbd.ShowDialog() == DialogResult.OK)
                textBox18.Text = fbd.SelectedPath;
            else
                textBox18.Text = string.Empty;
        }

        private void Readcode_Click(object sender, EventArgs e)
        {

            string str_outputFile_fullName = $"{textBox18.Text}\\{textBox17.Text}";

            string str_cmd = $"nrfjprog.exe {str_IC_number} --readcode  {str_outputFile_fullName} ";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.READCODE);
        }



        #endregion



        #region Ineer Usage

        public void ComboxsItemSettingsFormLoad()
        {
            //New Combox dictionary for multiTarget Selecting
            comboxDic = new Dictionary<int, ComboBox>();
            comboxDic.Add(1, comboBox1);
            comboxDic.Add(2, comboBox2);
            comboxDic.Add(3, comboBox3);
            comboxDic.Add(4, comboBox4);
            comboxDic.Add(5, comboBox5);
            comboxDic.Add(6, comboBox6);
            comboxDic.Add(7, comboBox7);
            comboxDic.Add(8, comboBox8);
            comboxDic.Add(9, comboBox9);
            comboxDic.Add(10, comboBox10);

            //clear combox items 
            for (int i = 1; i <= int_combox_num; i++)
            {
                comboxDic[i].Items.Clear();
                comboxDic[i].Items.Add(string.Empty);
            }
                


            //Add Combox content
            foreach (string str_serialNo in str_arr_serialsNOs)
            {
                if (!String.IsNullOrEmpty(str_serialNo))
                {
                    for (int i = 1; i <=int_combox_num; i++)
                    {
                        comboxDic[i].Items.Add(str_serialNo);
                    }
                }
            }


            //set default combox selected item
            for (int i = 1; i <= 10; i++)
            {
                comboxDic[i].SelectedIndex = int_combox_default_index;
            }

        }

        public void ReadAllSerialNO()
        {
            string str_cmd = $"nrfjprog.exe -i";
            string str_cmd_exe_path = $"cmd.exe";
            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.READ_ALL_SERIAL_NO,false);
        }

        public void ReadIcVersion(string str_jlink_serial_no="")
        {
            string str_ICVersion_address = $"0x10000100";
            string str_cmd = string.Empty;
            if (!String.IsNullOrEmpty(str_jlink_serial_no))
                str_cmd = $"nrfjprog.exe -s {str_jlink_serial_no} --memrd {str_ICVersion_address}  ";
            else
                str_cmd = $"nrfjprog.exe --memrd {str_ICVersion_address} ";
            string str_cmd_exe_path = $"cmd.exe";

            OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.READ_IC_VERSION,false);

        }

        public void OpenCmdAndExecute(string str_cmd_exe_path, string str_cmd, CMD_TYPE cmd_type, bool IsFormControl = true,bool IsLastControlItem=false)
        {

            //prevent user click another controls 
            if (IsFormControl)
            {
                //MessageBox.Show("disable form");
                Form_disable_delegation d = new Form_disable_delegation(this.Disable_Form);
                this.Invoke(d);
            }


            //open command line exe
            Process p = new Process();
            p.StartInfo.FileName = str_cmd_exe_path;

            //set cmd format
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.Arguments = $"/c {str_cmd}";
            p.Start();


            //write result
            string output = p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();

            //output handler
            OutPutHandle(output, err, str_cmd, cmd_type,IsFormControl,IsLastControlItem);

            //close process
            p.Close();



                


        }

        private void OutPutHandle(string output, string err, string cmd, CMD_TYPE type,bool IsFormControl = true,bool IsLastControlItem = false)
        {

            //error
            if (err != string.Empty)
            {
                MessageBox.Show($"Proccess failed! \r\nError : {err}\r\n");
                //prevent user click another controls 
                if (IsFormControl)
                {
                    //MessageBox.Show("enable form");
                    Form_enable_delegation d = new Form_enable_delegation(this.Enable_Form);
                    this.Invoke(d);
                }
                return;
            }

            switch (type)
            {
                case CMD_TYPE.RECOVER:
                    MessageBox.Show($"Recover proccess is done!");
                    break;

                case CMD_TYPE.RECOVER_MULTI:
                    if (IsLastControlItem)
                        MessageBox.Show($"Multi Recover proccesses are done!");
                    break;

                case CMD_TYPE.ERASE_ALL:
                    MessageBox.Show($"Erasing proccess is done!");
                    break;

                case CMD_TYPE.ERASE_MULTI_ALL:
                    if (IsLastControlItem)
                        MessageBox.Show($"Multi Erasing processes are done!");
                    
                    break;

                case CMD_TYPE.SYS_RESET:
                    MessageBox.Show($"System is reset\r\n");
                    break;

                case CMD_TYPE.SYS_MULTI_RESET:
                    if (IsLastControlItem)
                        MessageBox.Show($"Multi Systems are reset\r\n");
                    break;

                case CMD_TYPE.PROGRAM_SD:
                    MessageBox.Show($"Programing proccess is done\r\n");
                    break;

                case CMD_TYPE.PROGRAM_APP:
                    MessageBox.Show($"Programing proccess is done\r\n");
                    break;

                case CMD_TYPE.PROGRAM_MULTI_APP:
                    if (IsLastControlItem)
                    {
                        string str_jlnik_serialNo = cmd.Substring(cmd.IndexOf("-s") + 2, 10);
                        MessageBox.Show($"Multi Programing proccesses are done\r\n");
                    }

                    break;

                case CMD_TYPE.BT_ADDRESS_READ:

                    //Address Fixer
                    string str_first_address_char = Device_Address_Fixer(output.Substring(34, 1));
                    textBox5.Text =
                        $"{str_first_address_char}{output.Substring(35, 1)}:{output.Substring(36, 2)}:" +
                        $"{output.Substring(21, 2)}:{output.Substring(23, 2)}:" +
                        $"{output.Substring(25, 2)}:{output.Substring(27, 2)}";
                    MessageBox.Show($"BT address finded!");
                    break;

                case CMD_TYPE.UICR_VALUE_READ:
                    textBox3.Text =
                        $"{output.Substring(12, 2)}{output.Substring(14, 2)}" +
                        $"{output.Substring(16, 2)}{output.Substring(18, 2)}";
                    MessageBox.Show($"UICR Value is finded!");
                    break;

                case CMD_TYPE.UICR_VALUE_SET:
                    textBox3.Text = "";
                    textBox4.Text = "";
                    MessageBox.Show($"UICR Value is set!");
                    break;

                case CMD_TYPE.RBP:
                        MessageBox.Show($"Readback protection is applied!");
                    break;

                case CMD_TYPE.RBP_MULTI:
                    if (IsLastControlItem)
                        MessageBox.Show($"Multi Readback protections are applied!");
                    break;

                case CMD_TYPE.MERGE:
                    //if there is temp hex, delete it
                    if (File.Exists(str_temp_merge_outputFile_fullName))
                        File.Delete(str_temp_merge_outputFile_fullName);

                    MessageBox.Show($"Merge Proccess is done!");
                    break;

                case CMD_TYPE.MERGE_TEMP:
                    break;

                case CMD_TYPE.READCODE:
                    MessageBox.Show($"ReadCode Proccess is done!");
                    break;

                case CMD_TYPE.READ_IC_VERSION:
                    if (output.Contains("51"))
                        str_IC_number = "-f nrf51";
                    else if (output.Contains("52"))
                        str_IC_number = "-f nrf52";
                    else if (output.Contains("53"))
                        str_IC_number = "-f nrf53";
                    break;

                // Parse all Debugger serial no to MultiTarget Dropdown list
                case CMD_TYPE.READ_ALL_SERIAL_NO:
                    str_arr_serialsNOs = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    break;

                default:
                    break;
            }

            //prevent user click another controls 
            if (IsFormControl)
            {
                //MessageBox.Show("enable form");
                Form_enable_delegation d = new Form_enable_delegation(this.Enable_Form);
                this.Invoke(d);
            }
                



        }

        private void Combox_list_refresh(object sender, EventArrivedEventArgs e)
        {
            //read connected debugger serial numbers
            ReadAllSerialNO();
            ComboxsItemSettingsFormLoad();
        }

        private string Device_Address_Fixer(string str_hex_to_fix)
        {
            string result = string.Empty;
            string str_hex_to_binary = string.Empty;

            //Convert str_hex_to_fix to Binaray string 
            str_hex_to_binary = 
                String.Join(String.Empty,
                                str_hex_to_fix.Select(
                                c => Convert.ToString(Convert.ToInt32(c.ToString(), 16), 2).PadLeft(4, '0')
                                )
                            );

            //fix the first two bit to "1"
            str_hex_to_binary = $"11{str_hex_to_binary.Substring(2,2)}";

            //Convert it to hex string again
            result = Convert.ToInt32(str_hex_to_binary, 2).ToString("X");
            

            return result;
        }

        private void USB_Event_Watcher()
        {
            ManagementEventWatcher watcher = new ManagementEventWatcher();
            WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 Or EventType = 3 ");
            watcher.EventArrived += new EventArrivedEventHandler(Combox_list_refresh);
            watcher.Query = query;
            watcher.Start();
            //watcher.WaitForNextEvent();

        }


        #endregion















   

































        



    }




    // MultiThreadAction 
    public partial class MultiThreadsAction
    {
        private Form1 form;
        private string str_multi_IC_version = string.Empty;
        private string str_multi_Serial_NOs = string.Empty;
        private string str_multi_Filepath = string.Empty;
        private bool Is_multi_FormControl = false;
        private bool Is_Last_Control_Item = false;


        public MultiThreadsAction(Form1 call_form,string str_IC_version, string str_serial_NOs, string str_filepath, bool IsFormControl,bool IsLastControlItem)
        {
            this.form = call_form;
            this.str_multi_IC_version = str_IC_version;
            this.str_multi_Serial_NOs = str_serial_NOs;
            this.str_multi_Filepath = str_filepath;
            this.Is_multi_FormControl = IsFormControl;
            this.Is_Last_Control_Item = IsLastControlItem;
        }

        public void Reset_ic()
        {
            string str_cmd = $"nrfjprog.exe {str_multi_IC_version} -s {str_multi_Serial_NOs} -r";
            string str_cmd_exe_path = $"cmd.exe";

            form.OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.SYS_MULTI_RESET, Is_multi_FormControl,Is_Last_Control_Item);
        }

        public void Erase_all()
        {
            string str_cmd = $"nrfjprog.exe {str_multi_IC_version} -s {str_multi_Serial_NOs} -e";
            string str_cmd_exe_path = $"cmd.exe";
            form.OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.ERASE_MULTI_ALL, Is_multi_FormControl,Is_Last_Control_Item);
        }

        public void Program_Hex()
        {
            string str_cmd = $"nrfjprog.exe {str_multi_IC_version} -s {str_multi_Serial_NOs} --program {str_multi_Filepath} --verify";
            string str_cmd_exe_path = $"cmd.exe";

            form.OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.PROGRAM_MULTI_APP, Is_multi_FormControl,Is_Last_Control_Item);
        }

        public void RBP()
        {
            Program_Hex();

            string str_cmd = $"nrfjprog.exe {str_multi_IC_version} -s {str_multi_Serial_NOs} --rbp ALL";
            string str_cmd_exe_path = $"cmd.exe";

            form.OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.RBP_MULTI, Is_multi_FormControl,Is_Last_Control_Item);

        }

        public void Recover()
        {
            string str_cmd = $"nrfjprog.exe {str_multi_IC_version} -s {str_multi_Serial_NOs} --recover";
            string str_cmd_exe_path = $"cmd.exe";
            form.OpenCmdAndExecute(str_cmd_exe_path, str_cmd, CMD_TYPE.RECOVER_MULTI, Is_multi_FormControl, Is_Last_Control_Item);
        }
    }
}
