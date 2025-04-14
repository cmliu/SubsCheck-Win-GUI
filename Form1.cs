using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace subs_check.win.gui
{
    public partial class Form1: Form
    {
        //string 版本号;
        string 标题;
        private Process subsCheckProcess = null;
        string nodeInfo;//进度
        private Icon originalNotifyIcon; // 保存原始图标
        private ToolStripMenuItem startMenuItem;
        private ToolStripMenuItem stopMenuItem;
        string githubProxyURL = "";
        int run = 0;
        string 当前subsCheck版本号 = "未知版本";
        string 当前GUI版本号 = "未知版本";
        string 最新GUI版本号 = "未知版本";
        private string nextCheckTime = null;// 用于存储下次检查时间
        string WebUIapiKey = "CMLiussss";
        private CheckBox checkBoxAutoStart;
        public Form1()
        {
            InitializeComponent();
            originalNotifyIcon = notifyIcon1.Icon;

            toolTip1.SetToolTip(checkBoxAutoStart, "勾选后，程序启动时将自动开始检查节点");

            toolTip1.SetToolTip(numericUpDown1, "并发线程数：推荐 宽带峰值/50M。");
            toolTip1.SetToolTip(numericUpDown2, "检查间隔时间(分钟)：放置后台的时候，下次自动测速的间隔时间。\n\n 双击切换 使用「cron表达式」");
            toolTip1.SetToolTip(label2, "检查间隔时间(分钟)：放置后台的时候，下次自动测速的间隔时间。\n\n 双击切换 使用「cron表达式」");

            toolTip1.SetToolTip(numericUpDown3, "超时时间(毫秒)：节点的最大延迟。");
            toolTip1.SetToolTip(numericUpDown4, "最低测速结果舍弃(KB/s)。");
            toolTip1.SetToolTip(numericUpDown5, "下载测试时间(s)：与下载链接大小相关，默认最大测试10s。");
            toolTip1.SetToolTip(numericUpDown6, "本地监听端口：用于直接返回测速结果的节点信息，方便 Sub-Store 实现订阅转换。");
            toolTip1.SetToolTip(numericUpDown7, "Sub-Store监听端口：用于订阅订阅转换。\n注意：除非你知道你在干什么，否则不要将你的 Sub-Store 暴露到公网，否则可能会被滥用");
            toolTip1.SetToolTip(textBox1, "节点池订阅地址：支持 Link、Base64、Clash 格式的订阅链接。");
            toolTip1.SetToolTip(checkBox1, "以节点IP查询位置重命名节点。\n质量差的节点可能造成IP查询失败，造成整体检查速度稍微变慢。");
            toolTip1.SetToolTip(checkBox2, "是否开启流媒体检测，其中IP欺诈依赖'节点地址查询'，内核版本需要 v2.0.8 以上\n\n示例：美国1 | ⬇️ 5.6MB/s |0%|Netflix|Disney|Openai\n风控值：0% (使用ping0.cc标准)\n流媒体解锁：Netflix、Disney、Openai");
            toolTip1.SetToolTip(comboBox3, "GitHub 代理：代理订阅 GitHub raw 节点池。");
            toolTip1.SetToolTip(comboBox2, "测速地址：注意 并发数*节点速度<最大网速 否则测速结果不准确\n尽量不要使用Speedtest，Cloudflare提供的下载链接，因为很多节点屏蔽测速网站。");
            toolTip1.SetToolTip(textBox7, "将测速结果推送到Worker的地址。");
            toolTip1.SetToolTip(textBox6, "Worker令牌。");
            toolTip1.SetToolTip(comboBox1, "测速结果的保存方法。");
            toolTip1.SetToolTip(textBox2, "Gist ID：注意！非Github用户名！");
            toolTip1.SetToolTip(textBox3, "Github TOKEN");
            
            toolTip1.SetToolTip(comboBox4, "通用订阅：内置了Sub-Store程序，自适应订阅格式。\nClash订阅：带规则的 Mihomo、Clash 订阅格式。");
            toolTip1.SetToolTip(comboBox5, "生成带规则的 Clash 订阅所需的覆写规则文件");

            toolTip1.SetToolTip(checkBox3, "保存几个成功的节点，不选代表不限制，内核版本需要 v2.1.0 以上\n如果你的并发数量超过这个参数，那么成功的结果可能会大于这个数值");
            toolTip1.SetToolTip(numericUpDown8, "保存几个成功的节点，不选代表不限制，内核版本需要 v2.1.0 以上\n如果你的并发数量超过这个参数，那么成功的结果可能会大于这个数值");

            toolTip1.SetToolTip(textBox11, "支持标准cron表达式，如：\n 0 */2 * * * 表示每2小时的整点执行\n 0 0 */2 * * 表示每2天的0点执行\n 0 0 1 * * 表示每月1日0点执行\n */30 * * * * 表示每30分钟执行一次\n\n 双击切换 使用「分钟倒计时」");

            toolTip1.SetToolTip(checkBox5, "开机启动：勾选后，程序将在Windows启动时自动运行");
            // 设置通知图标的上下文菜单
            SetupNotifyIconContextMenu();
        }

        private void SetupNotifyIconContextMenu()
        {
            // 创建上下文菜单
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // 创建"▶️ 启动"菜单项
            startMenuItem = new ToolStripMenuItem("启动");
            startMenuItem.Click += (sender, e) =>
            {
                if (button1.Text == "▶️ 启动")
                {
                    button1_Click(sender, e);
                }
            };

            // 创建"⏹️ 停止"菜单项
            stopMenuItem = new ToolStripMenuItem("停止");
            stopMenuItem.Click += (sender, e) =>
            {
                if (button1.Text == "⏹️ 停止")
                {
                    button1_Click(sender, e);
                }
            };
            stopMenuItem.Enabled = false; // 初始状态下禁用

            // 创建"退出"菜单项
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("退出");
            exitMenuItem.Click += async (sender, e) =>
            {
                try
                {
                    // 如果程序正在运行，先停止它
                    if (subsCheckProcess != null && !subsCheckProcess.HasExited)
                    {
                        await KillNodeProcessAsync();
                        StopSubsCheckProcess();
                    }

                    // 确保通知图标被移除
                    notifyIcon1.Visible = false;

                    // 使用更直接的退出方式
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"退出程序时发生错误: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                    // 如果仍然失败，尝试强制退出
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
            };

            // 将菜单项添加到上下文菜单
            contextMenu.Items.Add(startMenuItem);
            contextMenu.Items.Add(stopMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator()); // 分隔线
            contextMenu.Items.Add(exitMenuItem);

            // 将上下文菜单分配给通知图标
            notifyIcon1.ContextMenuStrip = contextMenu;

            // 确保通知图标可见
            notifyIcon1.Visible = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // 取消关闭操作
                e.Cancel = true;
                // 调用隐藏窗口方法
                隐藏窗口();
            }
        }

        private async void timer1_Tick(object sender, EventArgs e)//初始化
        {
            timer1.Enabled = false;
            if (button2.Text == "高级设置∧") button2_Click(sender, e);

            // 检查实时启动设置
            if (checkBoxAutoStart.Checked && !CheckCommandLineParameter("-auto"))
            {
                button1_Click(this, EventArgs.Empty);
            }

            // 检查并创建config文件夹
            string executablePath = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            string configFolderPath = System.IO.Path.Combine(executablePath, "config");
            if (!System.IO.Directory.Exists(configFolderPath))
            {
                // 文件不存在，可以给用户反馈
                string 免责声明 = "SubsCheck-Win-GUI 项目仅供教育、研究和安全测试目的而设计和开发。本项目旨在为安全研究人员、学术界人士及技术爱好者提供一个探索和实践网络通信技术的工具。\r\n在下载和使用本项目代码时，使用者必须严格遵守其所适用的法律和规定。使用者有责任确保其行为符合所在地区的法律框架、规章制度及其他相关规定。\r\n\r\n使用条款\r\n\r\n教育与研究用途：本软件仅可用于网络技术和编程领域的学习、研究和安全测试。\r\n禁止非法使用：严禁将 SubsCheck-Win-GUI 用于任何非法活动或违反使用者所在地区法律法规的行为。\r\n使用时限：基于学习和研究目的，建议用户在完成研究或学习后，或在安装后的24小时内，删除本软件及所有相关文件。\r\n免责声明：SubsCheck-Win-GUI 的创建者和贡献者不对因使用或滥用本软件而导致的任何损害或法律问题负责。\r\n用户责任：用户对使用本软件的方式以及由此产生的任何后果完全负责。\r\n无技术支持：本软件的创建者不提供任何技术支持或使用协助。\r\n知情同意：使用 SubsCheck-Win-GUI 即表示您已阅读并理解本免责声明，并同意受其条款的约束。\r\n\r\n请记住：本软件的主要目的是促进学习、研究和安全测试。创作者不支持或认可任何其他用途。使用者应当在合法和负责任的前提下使用本工具。\r\n\r\n同意以上条款请点击\"是 / Yes\"，否则程序将退出。";

                // 显示带有 "同意" 和 "拒绝" 选项的对话框
                DialogResult result = MessageBox.Show(免责声明, "SubsCheck-Win-GUI 免责声明", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                // 如果用户点击 "拒绝" (对应于 No 按钮)
                if (result == DialogResult.No)
                {
                    // 退出程序
                    Environment.Exit(0); // 立即退出程序
                }
                System.IO.Directory.CreateDirectory(configFolderPath);
            }

            FileVersionInfo myFileVersionInfo = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            当前GUI版本号 = "v" + myFileVersionInfo.FileVersion;
            最新GUI版本号 = 当前GUI版本号;
            标题 = "SubsCheck Win GUI " + 当前GUI版本号;
            this.Text = 标题;// + " TG:CMLiussss BY:CM喂饭 干货满满";
            comboBox1.Text = "本地";
            comboBox4.Text = "通用订阅";
            ReadConfig();

            if (CheckCommandLineParameter("-auto"))
            {
                Log("检测到开机启动，准备执行任务...");
                button1_Click(this, EventArgs.Empty);
                this.Hide();
                notifyIcon1.Visible = true;
            } else await CheckGitHubVersionAsync();
        }

        private async Task CheckGitHubVersionAsync()
        {
            try
            {
                // 首先检查是否有网络连接
                if (!IsNetworkAvailable())
                {
                    return; // 静默返回，不显示错误
                }

                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                        client.Timeout = TimeSpan.FromSeconds(5); // 设置5秒超时

                        string url = "https://api.github.com/repos/cmliu/SubsCheck-Win-GUI/releases/latest";
                        HttpResponseMessage response = await client.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();
                            JObject json = JObject.Parse(responseBody);
                            string latestVersion = json["tag_name"].ToString();

                            if (latestVersion != 当前GUI版本号)
                            {
                                最新GUI版本号 = latestVersion;
                                标题 = "SubsCheck Win GUI " + 当前GUI版本号 + $"  发现新版本: {最新GUI版本号} 请及时更新！";
                                this.Text = 标题;
                            }
                        }
                    }
                    catch
                    {
                        // 静默处理所有异常（网络错误、超时、JSON解析错误等）
                        return;
                    }
                }
            }
            catch
            {
                // 静默处理任何其他异常
                return;
            }
        }

        // 添加检查网络连接的辅助方法
        private bool IsNetworkAvailable()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false; // 如果无法检查网络状态，假设网络不可用
            }
        }

        private async void ReadConfig()//读取配置文件
        {
            checkBox5.CheckedChanged -= checkBox5_CheckedChanged;// 临时移除事件处理器，防止触发事件
            try
            {
                string executablePath = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                string configFilePath = Path.Combine(executablePath, "config", "config.yaml");

                if (File.Exists(configFilePath))
                {
                    // 读取YAML文件内容
                    string yamlContent = File.ReadAllText(configFilePath);

                    // 使用YamlDotNet解析YAML
                    var deserializer = new YamlDotNet.Serialization.Deserializer();
                    var config = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                    // 使用新函数获取整数值并设置UI控件
                    int? concurrentValue = 读取config整数(config, "concurrent");
                    if (concurrentValue.HasValue) numericUpDown1.Value = concurrentValue.Value;

                    int? checkIntervalValue = 读取config整数(config, "check-interval");
                    if (checkIntervalValue.HasValue) numericUpDown2.Value = checkIntervalValue.Value;

                    int? timeoutValue = 读取config整数(config, "timeout");
                    if (timeoutValue.HasValue) numericUpDown3.Value = timeoutValue.Value;

                    int? minspeedValue = 读取config整数(config, "min-speed");
                    if (minspeedValue.HasValue) numericUpDown4.Value = minspeedValue.Value;

                    int? downloadtimeoutValue = 读取config整数(config, "download-timeout");
                    if (downloadtimeoutValue.HasValue) numericUpDown5.Value = downloadtimeoutValue.Value;

                    string speedTestUrl = 读取config字符串(config, "speed-test-url");
                    if (speedTestUrl != null) comboBox2.Text = speedTestUrl;

                    string savemethod = 读取config字符串(config, "save-method");
                    if (savemethod != null)
                    {
                        if (savemethod == "local") comboBox1.Text = "本地";
                        else comboBox1.Text = savemethod;
                    }

                    string listenport = 读取config字符串(config, "listen-port");
                    if (listenport != null)
                    {
                        // 查找最后一个冒号的位置
                        int colonIndex = listenport.LastIndexOf(':');
                        if (colonIndex >= 0 && colonIndex < listenport.Length - 1)
                        {
                            // 提取冒号后面的部分作为端口号
                            string portStr = listenport.Substring(colonIndex + 1);
                            if (decimal.TryParse(portStr, out decimal port))
                            {
                                numericUpDown6.Value = port;
                            }
                        }
                    }

                    /*
                    int? substoreport = 读取config整数(config, "sub-store-port");
                    if (substoreport.HasValue) numericUpDown7.Value = substoreport.Value;
                    */

                    string substoreport = 读取config字符串(config, "sub-store-port");
                    if (substoreport != null)
                    {
                        // 查找最后一个冒号的位置
                        int colonIndex = substoreport.LastIndexOf(':');
                        if (colonIndex >= 0 && colonIndex < substoreport.Length - 1)
                        {
                            // 提取冒号后面的部分作为端口号
                            string portStr = substoreport.Substring(colonIndex + 1);
                            if (decimal.TryParse(portStr, out decimal port))
                            {
                                numericUpDown7.Value = port;
                            }
                        }
                    }

                    string githubproxy = 读取config字符串(config, "githubproxy");
                    if (githubproxy != null) comboBox3.Text = githubproxy;

                    const string githubRawPrefix = "https://raw.githubusercontent.com/";

                    string mihomoOverwriteUrl = 读取config字符串(config, "mihomo-overwrite-url");
                    int mihomoOverwriteUrlIndex = mihomoOverwriteUrl.IndexOf(githubRawPrefix);
                    if (mihomoOverwriteUrl != null)
                    {
                        if (mihomoOverwriteUrl.Contains("http://127.0.0"))
                        {
                            if (mihomoOverwriteUrl.EndsWith("bdg.yaml", StringComparison.OrdinalIgnoreCase))
                            {
                                comboBox5.Text = "[内置]布丁狗的订阅转换";
                                await ProcessComboBox5Selection();
                            }
                            else if (mihomoOverwriteUrl.EndsWith("ACL4SSR_Online_Full.yaml", StringComparison.OrdinalIgnoreCase))
                            {
                                comboBox5.Text = "[内置]ACL4SSR_Online_Full";
                                await ProcessComboBox5Selection();
                            }
                        }
                        else if (mihomoOverwriteUrlIndex > 0) comboBox5.Text = mihomoOverwriteUrl.Substring(mihomoOverwriteUrlIndex);
                        else comboBox5.Text = mihomoOverwriteUrl;
                    }

                    // 处理URLs，检查是否包含GitHub raw链接
                    List<string> subUrls = 读取config列表(config, "sub-urls");
                    if (subUrls != null && subUrls.Count > 0)
                    {
                        // 创建一个新的过滤后的列表
                        var filteredUrls = new List<string>();

                        for (int i = 0; i < subUrls.Count; i++)
                        {
                            // 排除本地URL
                            string localUrlPattern = $"http://127.0.0.1:{numericUpDown6.Value}/all.yaml";
                            if (!subUrls[i].Equals(localUrlPattern, StringComparison.OrdinalIgnoreCase))
                            {
                                // 处理GitHub raw链接
                                int index = subUrls[i].IndexOf(githubRawPrefix);
                                if (index > 0) // 如果找到且不在字符串开头
                                {
                                    // 只保留从githubRawPrefix开始的部分
                                    filteredUrls.Add(subUrls[i].Substring(index));
                                }
                                else
                                {
                                    // 如果不是GitHub链接，直接添加
                                    filteredUrls.Add(subUrls[i]);
                                }
                            }
                        }

                        // 将过滤后的列表中的每个URL放在单独的行上
                        textBox1.Text = string.Join(Environment.NewLine, filteredUrls);
                    }

                    string renamenode = 读取config字符串(config, "rename-node");
                    if (renamenode != null && renamenode == "true") checkBox1.Checked = true;
                    else checkBox1.Checked = false;

                    string mediacheck = 读取config字符串(config, "media-check");
                    if (mediacheck != null && mediacheck == "true") checkBox2.Checked = true;
                    else checkBox2.Checked = false;

                    string githubgistid = 读取config字符串(config, "github-gist-id");
                    if (githubgistid != null) textBox2.Text = githubgistid;
                    string githubtoken = 读取config字符串(config, "github-token");
                    if (githubtoken != null) textBox3.Text = githubtoken;

                    string githubapimirror = 读取config字符串(config, "github-api-mirror");
                    if (githubapimirror != null) textBox4.Text = githubapimirror;

                    string workerurl = 读取config字符串(config, "worker-url");
                    if (workerurl != null) textBox7.Text = workerurl;
                    string workertoken = 读取config字符串(config, "worker-token");
                    if (workertoken != null) textBox6.Text = workertoken;

                    string webdavusername = 读取config字符串(config, "webdav-username");
                    if (webdavusername != null) textBox9.Text = webdavusername;
                    string webdavpassword = 读取config字符串(config, "webdav-password");
                    if (webdavpassword != null) textBox8.Text = webdavpassword;
                    string webdavurl = 读取config字符串(config, "webdav-url");
                    if (webdavurl != null) textBox5.Text = webdavurl;

                    string subscheckversion = 读取config字符串(config, "subscheck-version");
                    if (subscheckversion != null) 当前subsCheck版本号 = subscheckversion;

                    int? successlimit = 读取config整数(config, "success-limit");
                    if (successlimit.HasValue)
                    {
                        if (successlimit.Value == 0)
                        {
                            checkBox3.Checked = false;
                            numericUpDown8.Enabled = false;
                        }
                        else
                        {
                            checkBox3.Checked = true;
                            numericUpDown8.Enabled = true;
                            numericUpDown8.Value = successlimit.Value;
                        }
                    }

                    string enablewebui = 读取config字符串(config, "enable-web-ui");
                    if (enablewebui != null && enablewebui == "true") checkBox4.Checked = true;
                    else checkBox4.Checked = false;

                    string autoStart = 读取config字符串(config, "auto-start");
                    if (autoStart != null && autoStart == "true") 
                        {
                            checkBoxAutoStart.Checked = true;
                            Log("从配置中读取到实时启动已启用，将自动检测节点并执行任务");
                        }
                         else
                        {
                            checkBoxAutoStart.Checked = false;
                            Log("从配置中读取到实时启动已禁用，将不会自动检测节点，需点击启动执行任务");
                        }

                    string apikey = 读取config字符串(config, "api-key");
                    if (apikey != null)
                    {
                        if (apikey == GetComputerNameMD5())
                        {
                            checkBox4.Checked = false;
                            string oldapikey = 读取config字符串(config, "old-api-key");
                            if (oldapikey != null)
                            {
                                textBox10.Text = oldapikey;
                            }
                            else
                            {
                                textBox10.PasswordChar = '\0';
                                textBox10.Text = "请输入密钥";
                                textBox10.ForeColor = Color.Gray;
                            }
                        }
                        else
                        {
                            textBox10.Text = apikey;
                        }
                    }

                    string cronexpression = 读取config字符串(config, "cron-expression");
                    if (cronexpression != null)
                    {
                        textBox11.Text = cronexpression;
                        string cronDescription = GetCronExpressionDescription(textBox11.Text);
                        textBox11.Location = new Point(9, 48);
                        textBox11.Visible = true;
                        label2.Visible = false;
                        numericUpDown2.Visible = false;
                    }

                    string guiauto = 读取config字符串(config, "gui-auto");
                    if (guiauto != null && guiauto == "true") checkBox5.Checked = true;
                    else checkBox5.Checked = false;
                }
                else
                {
                    comboBox3.Text = "自动选择";
                    comboBox5.Text = "[内置]布丁狗的订阅转换";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取配置文件时发生错误: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            checkBox5.CheckedChanged += checkBox5_CheckedChanged;// 重新绑定事件处理器
        }

        private int? 读取config整数(Dictionary<string, object> config, string fieldName)
        {
            // 检查是否存在指定字段且不为null
            if (config.ContainsKey(fieldName) && config[fieldName] != null)
            {
                int value;
                if (int.TryParse(config[fieldName].ToString(), out value))
                    return value;
            }
            return null;
        }

        private string 读取config字符串(Dictionary<string, object> config, string fieldName)
        {
            // 检查是否存在指定字段且不为null
            if (config.ContainsKey(fieldName) && config[fieldName] != null)
            {
                return config[fieldName].ToString();
            }
            return null;
        }

        private List<string> 读取config列表(Dictionary<string, object> config, string fieldName)
        {
            // 检查是否存在指定字段且不为null
            if (config.ContainsKey(fieldName) && config[fieldName] != null)
            {
                // 尝试将对象转换为列表
                if (config[fieldName] is List<object> listItems)
                {
                    return listItems.Select(item => item?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                }
            }
            return null;
        }

        private async Task SaveConfig(bool githubProxyCheck = true)//保存配置文件
        {
            try
            {
                string executablePath = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                string configFilePath = Path.Combine(executablePath, "config", "config.yaml");

                // 创建配置字典
                var config = new Dictionary<string, object>();

                // 从UI控件获取值并添加到字典中
                config["concurrent"] = (int)numericUpDown1.Value;
                config["check-interval"] = (int)numericUpDown2.Value;
                if (textBox11.Visible) config["cron-expression"] = textBox11.Text;
                config["timeout"] = (int)numericUpDown3.Value;
                config["min-speed"] = (int)numericUpDown4.Value;
                config["download-timeout"] = (int)numericUpDown5.Value;

                if (!string.IsNullOrEmpty(comboBox2.Text)) config["speed-test-url"] = comboBox2.Text;

                // 保存save-method，将"本地"转换为"local"
                config["save-method"] = comboBox1.Text == "本地" ? "local" : comboBox1.Text;

                // 保存gist参数
                config["github-gist-id"] = textBox2.Text;
                config["github-token"] = textBox3.Text;

                config["github-api-mirror"] = textBox4.Text;

                // 保存r2参数
                config["worker-url"] = textBox7.Text;
                config["worker-token"] = textBox6.Text;

                // 保存webdav参数
                config["webdav-username"] = textBox9.Text;
                config["webdav-password"] = textBox8.Text;
                config["webdav-url"] = textBox5.Text;

                // 保存enable-web-ui
                config["enable-web-ui"] = true;

                // 保存listen-port
                if (checkBox4.Checked) 
                {
                    WebUIapiKey = textBox10.Text;
                    config["listen-port"] = $@":{numericUpDown6.Value}";
                }
                else
                {
                    WebUIapiKey = GetComputerNameMD5();
                    config["listen-port"] = $@"127.0.0.1:{numericUpDown6.Value}";
                    if (textBox10.Text != "请输入密钥") config["old-api-key"] = textBox10.Text;
                }
                config["api-key"] = WebUIapiKey;

                // 保存sub-store-port
                config["sub-store-port"] = $@":{numericUpDown7.Value}";
                // 是否开机自启
                config["rename-node"] = checkBox1.Checked? "true" : "false";
                config["gui-auto"] = checkBox5.Checked;//是否开机自启
                config["auto-start"] = checkBoxAutoStart.Checked; // 程序启动时自动运行


                string githubRawPrefix = "https://raw.githubusercontent.com/";
                if (githubProxyCheck)
                {
                    // 检查并处理 GitHub Raw URLs
                    if (comboBox3.Text == "自动选择")
                    {
                        // 创建不包含"自动选择"的代理列表
                        List<string> proxyItems = new List<string>();
                        for (int j = 0; j < comboBox3.Items.Count; j++)
                        {
                            string proxyItem = comboBox3.Items[j].ToString();
                            if (proxyItem != "自动选择")
                                proxyItems.Add(proxyItem);
                        }

                        // 随机打乱列表顺序
                        Random random = new Random();
                        proxyItems = proxyItems.OrderBy(x => random.Next()).ToList();

                        // 异步检测可用代理
                        githubProxyURL = await DetectGitHubProxyAsync(proxyItems);
                    }
                }

                if (comboBox3.Text != "自动选择") githubProxyURL = $"https://{comboBox3.Text}/";
                config["githubproxy"] = comboBox3.Text;
                config["github-proxy"] = githubProxyURL;

                // 保存sub-urls列表
                List<string> subUrls = new List<string>();
                string allyamlFilePath = System.IO.Path.Combine(executablePath, "output", "all.yaml");
                if (System.IO.File.Exists(allyamlFilePath))
                {
                    subUrls.Add($"http://127.0.0.1:{numericUpDown6.Value}/all.yaml");
                    //Log("已加载上次测试结果。");
                }

                if (!string.IsNullOrEmpty(textBox1.Text))
                {
                    subUrls.AddRange(textBox1.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList());
                    // 处理URLs
                    for (int i = 0; i < subUrls.Count; i++)
                    {
                        if (subUrls[i].StartsWith(githubRawPrefix) && !string.IsNullOrEmpty(githubProxyURL))
                        {
                            // 替换为代理 URL 格式
                            //subUrls[i] = githubProxyURL + githubRawPrefix + subUrls[i].Substring(githubRawPrefix.Length);
                            // 使用subs-check内置github-proxy参数
                            subUrls[i] = githubRawPrefix + subUrls[i].Substring(githubRawPrefix.Length);
                        }
                    }
                }

                config["sub-urls"] = subUrls;

                // 处理配置文件下载与配置
                if (comboBox5.Text.Contains("[内置]"))
                {
                    // 确定文件名和下载URL
                    string fileName;
                    string downloadFilePath;
                    string downloadUrl;
                    string displayName;

                    if (comboBox5.Text.Contains("[内置]布丁狗"))
                    {
                        fileName = "bdg.yaml";
                        displayName = "[内置]布丁狗的订阅转换";
                        downloadUrl = "https://raw.githubusercontent.com/cmliu/ACL4SSR/main/yaml/bdg.yaml";
                    }
                    else // [内置]ACL4SSR
                    {
                        fileName = "ACL4SSR_Online_Full.yaml";
                        displayName = "[内置]ACL4SSR_Online_Full";
                        downloadUrl = "https://raw.githubusercontent.com/beck-8/override-hub/main/yaml/ACL4SSR_Online_Full.yaml";
                    }

                    // 确保output文件夹存在
                    string outputFolderPath = Path.Combine(executablePath, "output");
                    if (!Directory.Exists(outputFolderPath))
                    {
                        Directory.CreateDirectory(outputFolderPath);
                    }

                    // 确定文件完整路径
                    downloadFilePath = Path.Combine(outputFolderPath, fileName);
                    if (!File.Exists(downloadFilePath)) await ProcessComboBox5Selection();

                    // 检查文件是否存在
                    if (!File.Exists(downloadFilePath))
                    {
                        Log($"{displayName} 覆写配置文件 未找到，将使用在线版本。");
                        config["mihomo-overwrite-url"] = githubProxyURL + downloadUrl;
                    }
                    else
                    {
                        Log($"{displayName} 覆写配置文件 加载成功。");
                        config["mihomo-overwrite-url"] = $"http://127.0.0.1:{numericUpDown6.Value}/{fileName}";
                    }
                }
                else if (comboBox5.Text.StartsWith(githubRawPrefix)) config["mihomo-overwrite-url"] = githubProxyURL + comboBox5.Text;
                else config["mihomo-overwrite-url"] = comboBox5.Text != "" ? comboBox5.Text : $"http://127.0.0.1:{numericUpDown6.Value}/ACL4SSR_Online_Full.yaml";
                
                config["rename-node"] = checkBox1.Checked;//以节点IP查询位置重命名节点
                config["media-check"] = checkBox2.Checked;//是否开启流媒体检测
                config["keep-success-proxies"] = false;
                config["print-progress"] = false;//是否显示进度
                config["sub-urls-retry"] = 3;//重试次数(获取订阅失败后重试次数)
                config["subscheck-version"] = 当前subsCheck版本号;//当前subsCheck版本号

                config["gui-auto"] = checkBox5.Checked;//是否开机自启

                //保存几个成功的节点，为0代表不限制 
                if (checkBox3.Checked) config["success-limit"] = (int)numericUpDown8.Value;
                else config["success-limit"] = 0;

                // 使用YamlDotNet序列化配置
                var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                    .WithIndentedSequences()  // 使序列化结果更易读
                    .Build();

                string yamlContent = serializer.Serialize(config);

                // 确保配置目录存在
                string configDirPath = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(configDirPath))
                    Directory.CreateDirectory(configDirPath);

                string moreYamlPath = Path.Combine(configDirPath, "more.yaml");
                if (File.Exists(moreYamlPath))
                {
                    // 读取more.yaml的内容
                    string moreYamlContent = File.ReadAllText(moreYamlPath);

                    // 确保more.yaml内容以换行开始
                    if (!moreYamlContent.StartsWith("\n") && !moreYamlContent.StartsWith("\r\n"))
                    {
                        yamlContent += "\n"; // 添加换行符作为分隔
                    }

                    // 将more.yaml的内容追加到要写入的config.yaml内容后
                    yamlContent += moreYamlContent;

                    Log($"已将补充参数配置 more.yaml 内容追加到配置文件");
                }
                // 写入YAML文件
                File.WriteAllText(configFilePath, yamlContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置文件时发生错误: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (button2.Text == "高级设置∧")
            {
                button2.Text = "高级设置∨";
                groupBox3.Visible = false;
            }
            else
            {
                button2.Text = "高级设置∧";
                groupBox3.Visible = true;
            }
            判断保存类型();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            
            if (button1.Text == "▶️ 启动") 
            {
                if (checkBox4.Checked && textBox10.Text == "请输入密钥")
                {
                    MessageBox.Show("您已启用WebUI，请设置WebUI API密钥！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                run = 1;
                if (button3.Enabled==false)
                {
                    string executablePath = Path.GetDirectoryName(Application.ExecutablePath);
                    string allyamlFilePath = Path.Combine(executablePath, "output", "all.yaml");
                    // 添加检查并提示
        if (checkBox5.Checked && File.Exists(allyamlFilePath))
        {
            Log("已加载上次测试结果。");
        }
                    button3.Enabled = File.Exists(allyamlFilePath);
                }

                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;
                textBox11.Enabled = false;
                numericUpDown3.Enabled = false;
                numericUpDown4.Enabled = false;
                numericUpDown5.Enabled = false;
                numericUpDown6.Enabled = false;
                numericUpDown7.Enabled = false;
                comboBox1.Enabled = false;
                textBox1.Enabled = false;
                groupBox3.Enabled = false;
                groupBox4.Enabled = false;
                groupBox5.Enabled = false;
                groupBox6.Enabled = false;
                if (checkBox4.Checked) button6.Enabled = true;
                button1.Text = "⏹️ 停止";
                timer3.Enabled = true;
                // 清空 richTextBox1
                richTextBox1.Clear();
                await KillNodeProcessAsync();
                await SaveConfig();

                if (run == 1) 
                {
                    // 更新菜单项的启用状态
                    startMenuItem.Enabled = false;
                    stopMenuItem.Enabled = true;

                    // 清空 richTextBox1
                    //richTextBox1.Clear();

                    notifyIcon1.Text = "SubsCheck: 已就绪";

                    // 启动 subs-check.exe 程序
                    StartSubsCheckProcess();
                }
            }
            else
            {
                run = 0;
                Log("任务停止");
                progressBar1.Value = 0;
                groupBox2.Text = "实时日志";
                notifyIcon1.Text = "SubsCheck: 未运行";
                // 停止 subs-check.exe 程序
                StopSubsCheckProcess();
                // 结束 Sub-Store
                await KillNodeProcessAsync();
                if (checkBox4.Checked) ReadConfig();
                button3.Enabled = false;
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                textBox11.Enabled = true;
                numericUpDown3.Enabled = true;
                numericUpDown4.Enabled = true;
                numericUpDown5.Enabled = true;
                numericUpDown6.Enabled = true;
                numericUpDown7.Enabled = true;
                comboBox1.Enabled = true;
                textBox1.Enabled = true;
                groupBox3.Enabled = true;
                groupBox4.Enabled = true;
                groupBox5.Enabled = true;
                groupBox6.Enabled = true;
                button6.Enabled = false;
                button1.Text = "▶️ 启动";
                timer3.Enabled = false;
                // 更新菜单项的启用状态
                startMenuItem.Enabled = true;
                stopMenuItem.Enabled = false;
            }
        }

        private async Task DownloadSubsCheckEXE()
        {
            button1.Enabled = false;

            try
            {
                Log("正在检查网络连接...");

                // 首先检查是否有网络连接
                if (!IsNetworkAvailable())
                {
                    Log("网络连接不可用，无法下载核心文件。", true);
                    MessageBox.Show("缺少 subs-check.exe 核心文件。\n\n您可以前往 https://github.com/beck-8/subs-check/releases 自行下载！",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 创建不使用系统代理的 HttpClientHandler
                HttpClientHandler handler = new HttpClientHandler
                {
                    UseProxy = false,
                    Proxy = null
                };

                // 使用自定义 handler 创建 HttpClient
                using (HttpClient client = new HttpClient(handler))
                {
                    try
                    {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win32; x86) AppleWebKit/537.36 (KHTML, like Gecko) cmliu/SubsCheck-Win-GUI");
                        client.Timeout = TimeSpan.FromSeconds(30); // 增加超时时间以适应下载需求

                        Log("正在获取最新版本 subs-check.exe 内核下载地址...");
                        string url = "https://api.github.com/repos/beck-8/subs-check/releases/latest";

                        // 使用异步方法
                        HttpResponseMessage response = await client.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            // 异步读取内容
                            string responseBody = await response.Content.ReadAsStringAsync();
                            JObject json = JObject.Parse(responseBody);
                            string latestVersion = json["tag_name"].ToString();
                            Log($"subs-check.exe 最新版本为: {latestVersion} ");
                            // 查找Windows i386版本的资源
                            string downloadUrl = null;
                            JArray assets = (JArray)json["assets"];
                            foreach (var asset in assets)
                            {
                                if (asset["name"].ToString() == "subs-check_Windows_i386.zip")
                                {
                                    downloadUrl = asset["browser_download_url"].ToString();
                                    break;
                                }
                            }

                            if (downloadUrl != null)
                            {
                                string 代理下载链接 = githubProxyURL + downloadUrl;
                                string 原生下载链接 = 代理下载链接;
                                // 计算"https://"在下载链接中出现的次数
                                int httpsCount = 0;
                                int lastIndex = -1;
                                int currentIndex = 0;

                                // 查找所有"https://"出现的位置
                                while ((currentIndex = 代理下载链接.IndexOf("https://", currentIndex)) != -1)
                                {
                                    httpsCount++;
                                    lastIndex = currentIndex;
                                    currentIndex += 8; // "https://".Length = 8
                                }

                                // 如果"https://"出现2次或以上，提取最后一个"https://"之后的内容
                                if (httpsCount >= 2 && lastIndex != -1)
                                {
                                    原生下载链接 = 代理下载链接.Substring(lastIndex);
                                }

                                string executablePath = Path.GetDirectoryName(Application.ExecutablePath);

                                // 创建下载请求 - 优化的多级尝试下载逻辑
                                Log("开始下载文件...");
                                bool downloadSuccess = false;
                                string zipFilePath = Path.Combine(executablePath, "subs-check_Windows_i386.zip");
                                string failureReason = "";

                                // 如果文件已存在，先删除
                                if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

                                // 第一次尝试：使用代理下载链接 + 当前HttpClient(不使用系统代理)
                                try
                                {
                                    Log($"[尝试1/4] 使用代理下载链接：{代理下载链接}");
                                    downloadSuccess = await DownloadFileAsync(client, 代理下载链接, zipFilePath);
                                }
                                catch (Exception ex)
                                {
                                    Log($"[尝试1/4] 失败: {ex.Message}", true);
                                    failureReason = ex.Message;
                                }

                                // 如果第一次尝试失败，且代理链接与原生链接不同，使用原生下载链接尝试
                                if (!downloadSuccess && 代理下载链接 != 原生下载链接)
                                {
                                    try
                                    {
                                        Log($"[尝试2/4] 使用原生下载链接：{原生下载链接}");
                                        downloadSuccess = await DownloadFileAsync(client, 原生下载链接, zipFilePath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"[尝试2/4] 失败: {ex.Message}", true);
                                        failureReason = ex.Message;
                                    }
                                }

                                // 如果前面的尝试都失败，创建使用系统代理的HttpClient再次尝试
                                if (!downloadSuccess)
                                {
                                    try
                                    {
                                        Log("[尝试3/4] 使用系统代理 + 代理下载链接");
                                        using (HttpClient proxyClient = new HttpClient())
                                        {
                                            proxyClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win32; x86) AppleWebKit/537.36 (KHTML, like Gecko) cmliu/SubsCheck-Win-GUI");
                                            proxyClient.Timeout = TimeSpan.FromSeconds(30);

                                            downloadSuccess = await DownloadFileAsync(proxyClient, 代理下载链接, zipFilePath);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"[尝试3/4] 失败: {ex.Message}", true);
                                        failureReason = ex.Message;
                                    }

                                    // 最后一次尝试：使用系统代理 + 原生链接（如果不同）
                                    if (!downloadSuccess && 代理下载链接 != 原生下载链接)
                                    {
                                        try
                                        {
                                            Log("[尝试4/4] 使用系统代理 + 原生下载链接");
                                            using (HttpClient proxyClient = new HttpClient())
                                            {
                                                proxyClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win32; x86) AppleWebKit/537.36 (KHTML, like Gecko) cmliu/SubsCheck-Win-GUI");
                                                proxyClient.Timeout = TimeSpan.FromSeconds(30);

                                                downloadSuccess = await DownloadFileAsync(proxyClient, 原生下载链接, zipFilePath);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"[尝试4/4] 失败: {ex.Message}", true);
                                            failureReason = ex.Message;
                                        }
                                    }
                                }

                                if (downloadSuccess)
                                {
                                    Log("下载完成，正在解压文件...");

                                    // 解压文件的代码保持不变
                                    using (System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(zipFilePath))
                                    {
                                        // 查找subs-check.exe
                                        System.IO.Compression.ZipArchiveEntry exeEntry = archive.Entries.FirstOrDefault(
                                            entry => entry.Name.Equals("subs-check.exe", StringComparison.OrdinalIgnoreCase));

                                        if (exeEntry != null)
                                        {
                                            string exeFilePath = Path.Combine(executablePath, "subs-check.exe");

                                            // 如果文件已存在，先删除
                                            if (File.Exists(exeFilePath))
                                            {
                                                File.Delete(exeFilePath);
                                            }

                                            // 解压文件
                                            exeEntry.ExtractToFile(exeFilePath);
                                            当前subsCheck版本号 = latestVersion;
                                            Log($"subs-check.exe {当前subsCheck版本号} 已就绪！");

                                            await SaveConfig(false);

                                            // 删除下载的zip文件
                                            //File.Delete(zipFilePath);
                                        }
                                        else
                                        {
                                            Log("无法在压缩包中找到 subs-check.exe 文件。", true);
                                        }
                                    }
                                }
                                else
                                {
                                    // 所有尝试都失败
                                    Log($"所有下载尝试均失败，最后错误: {failureReason}", true);
                                    MessageBox.Show($"下载 subs-check.exe 失败，请检查网络连接后重试。\n\n可尝试更换 Github Proxy 后，点击「检查更新」>「更新内核」。\n或前往 https://github.com/beck-8/subs-check/releases 自行下载！",
                                        "下载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    progressBar1.Value = 0;
                                }

                                // 解压文件
                                using (System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(zipFilePath))
                                {
                                    // 查找subs-check.exe
                                    System.IO.Compression.ZipArchiveEntry exeEntry = archive.Entries.FirstOrDefault(
                                        entry => entry.Name.Equals("subs-check.exe", StringComparison.OrdinalIgnoreCase));

                                    if (exeEntry != null)
                                    {
                                        string exeFilePath = Path.Combine(executablePath, "subs-check.exe");

                                        // 如果文件已存在，先删除
                                        if (File.Exists(exeFilePath))
                                        {
                                            File.Delete(exeFilePath);
                                        }

                                        // 解压文件
                                        exeEntry.ExtractToFile(exeFilePath);
                                        当前subsCheck版本号 = latestVersion;
                                        Log($"subs-check.exe {当前subsCheck版本号} 已就绪！");

                                        await SaveConfig(false);
                                        // 这里保留原有行为，不修改button1.Enabled

                                        // 删除下载的zip文件
                                        //File.Delete(zipFilePath);
                                    }
                                    else
                                    {
                                        Log("无法在压缩包中找到 subs-check.exe 文件。", true);
                                    }
                                }
                            }
                            else
                            {
                                Log("无法找到适用于 Windows i386 的下载链接。", true);
                                MessageBox.Show("未能找到适用的 subs-check.exe 下载链接。\n\n可尝试更换 Github Proxy 后，点击「检查更新」>「更新内核」。\n或前往 https://github.com/beck-8/subs-check/releases 自行下载！",
                                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            Log($"获取版本信息失败: HTTP {(int)response.StatusCode} {response.ReasonPhrase}", true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"下载过程中出错: {ex.Message}", true);
                        MessageBox.Show($"下载 subs-check.exe 时出错: {ex.Message}\n\n可尝试更换 Github Proxy 后，点击「检查更新」>「更新内核」。\n或前往 https://github.com/beck-8/subs-check/releases 自行下载！",
                            "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"初始化下载过程出错: {ex.Message}", true);
                MessageBox.Show($"下载准备过程出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            button1.Enabled = true;
        }


        private async void StartSubsCheckProcess()
        {
            try
            {
                // 重置进度条
                progressBar1.Value = 0;
                groupBox2.Text = "实时日志";
                using (MemoryStream ms = new MemoryStream(Properties.Resources.going))
                {
                    notifyIcon1.Icon = new Icon(ms);
                }

                // 获取当前应用程序目录
                string executablePath = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                string subsCheckPath = Path.Combine(executablePath, "subs-check.exe");

                // 检查是否有其他subs-check.exe进程正在运行，并强制结束它们
                try
                {
                    Process[] processes = Process.GetProcessesByName("subs-check");
                    if (processes.Length > 0)
                    {
                        Log("发现正在运行的subs-check.exe进程，正在强制结束...");
                        foreach (Process process in processes)
                        {
                            // 确保不是当前应用程序的进程
                            if (process != subsCheckProcess)
                            {
                                try
                                {
                                    process.Kill();
                                    process.WaitForExit();
                                    Log($"成功结束subs-check.exe进程(ID: {process.Id})");
                                }
                                catch (Exception ex)
                                {
                                    Log($"结束subs-check.exe进程时出错(ID: {process.Id}): {ex.Message}", true);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"检查运行中的subs-check.exe进程时出错: {ex.Message}", true);
                }

                // 检查文件是否存在
                if (!File.Exists(subsCheckPath))
                {
                    Log("没有找到 subs-check.exe 文件。", true);
                    await DownloadSubsCheckEXE(); // 使用异步等待
                }

                // 创建进程启动信息
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = subsCheckPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = executablePath,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                // 创建进程
                subsCheckProcess = new Process { StartInfo = startInfo };

                // 设置输出和错误数据接收事件处理
                subsCheckProcess.OutputDataReceived += SubsCheckProcess_OutputDataReceived;
                subsCheckProcess.ErrorDataReceived += SubsCheckProcess_OutputDataReceived;

                // 启动进程
                subsCheckProcess.Start();

                // 开始异步读取输出
                subsCheckProcess.BeginOutputReadLine();
                subsCheckProcess.BeginErrorReadLine();

                // 设置进程退出事件处理
                subsCheckProcess.EnableRaisingEvents = true;
                subsCheckProcess.Exited += SubsCheckProcess_Exited;

                Log($"subs-check.exe {当前subsCheck版本号} 已启动...");
                timer4.Enabled = true;
            }
            catch (Exception ex)
            {
                Log($"启动 subs-check.exe 时出错: {ex.Message}", true);
                button1.Text = "▶️ 启动";
            }
        }


        private void StopSubsCheckProcess()
        {
            timer4.Enabled = false;
            if (subsCheckProcess != null && !subsCheckProcess.HasExited)
            {
                try
                {
                    // 尝试正常关闭进程
                    subsCheckProcess.Kill();
                    subsCheckProcess.WaitForExit();
                    Log("subs-check.exe 已停止");
                    notifyIcon1.Icon = originalNotifyIcon;
                    button7.Enabled = false;
                    button7.Text = "🔀未启动";
                }
                catch (Exception ex)
                {
                    Log($"停止 subs-check.exe 时出错: {ex.Message}", true);
                }
                finally
                {
                    subsCheckProcess.Dispose();
                    subsCheckProcess = null;
                }
            }
        }
        
        private void SubsCheckProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // 由于此事件在另一个线程中触发，需要使用 Invoke 在 UI 线程上更新控件
                BeginInvoke(new Action(() =>
                {
                    // 过滤ANSI转义序列
                    string cleanText = RemoveAnsiEscapeCodes(e.Data);

                    // 检查是否包含"下次检查时间"信息
                    if (cleanText.Contains("下次检查时间:"))
                    {
                        if (button3.Enabled == false)
                        {
                            string executablePath = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                            string outputFolderPath = System.IO.Path.Combine(executablePath, "output");
                            if (System.IO.Directory.Exists(outputFolderPath))
                            {
                                string allyamlFilePath = System.IO.Path.Combine(outputFolderPath, "all.yaml");
                                if (System.IO.File.Exists(allyamlFilePath)) button3.Enabled = true;
                            }
                        }
                        // 提取完整的下次检查时间信息
                        int startIndex = cleanText.IndexOf("下次检查时间:");
                        nextCheckTime = cleanText.Substring(startIndex);
                    }

                    if (!cleanText.StartsWith("[GIN]"))
                    {
                        // 如果不是进度行，则添加到日志中
                        richTextBox1.AppendText(cleanText + "\r\n");
                        // 滚动到最底部
                        richTextBox1.SelectionStart = richTextBox1.Text.Length;
                        richTextBox1.ScrollToCaret();
                    }

                    /*
                    // 检查是否是进度信息行
                    if (cleanText.StartsWith("进度: ["))
                    {
                        // 解析百分比
                        int percentIndex = cleanText.IndexOf('%');
                        if (percentIndex > 0)
                        {
                            // 查找百分比前面的数字部分
                            int startIndex = cleanText.LastIndexOfAny(new char[] { ' ', '>' }, percentIndex) + 1;
                            string percentText = cleanText.Substring(startIndex, percentIndex - startIndex);

                            if (double.TryParse(percentText, out double percentValue))
                            {
                                // 更新进度条，将百分比值（0-100）设置给进度条
                                progressBar1.Value = (int)Math.Round(percentValue);
                            }
                        }

                        // 解析节点信息部分（例如：(12/6167) 可用: 0）
                        int infoStartIndex = cleanText.IndexOf('(');
                        if (infoStartIndex > 0)
                        {
                            string fullNodeInfo = cleanText.Substring(infoStartIndex);

                            // 提取最重要的信息：节点数量和可用数量
                            int endIndex = fullNodeInfo.IndexOf("2025-"); // 查找日期部分开始位置
                            if (endIndex > 0)
                            {
                                nodeInfo = fullNodeInfo.Substring(0, endIndex).Trim();
                            }
                            else
                            {
                                // 如果找不到日期部分，则取前30个字符
                                nodeInfo = fullNodeInfo.Length > 30 ? fullNodeInfo.Substring(0, 30) + "..." : fullNodeInfo;
                            }

                            groupBox2.Text = "实时日志 " + nodeInfo;

                            // 确保通知图标文本不超过63个字符
                            string notifyText = "SubsCheck: " + nodeInfo;
                            if (notifyText.Length > 63)
                            {
                                notifyText = notifyText.Substring(0, 60) + "...";
                            }
                            notifyIcon1.Text = notifyText;
                        }

                        // 更新lastProgressLine，但不向richTextBox添加文本
                        lastProgressLine = cleanText;
                    }
                    else
                    {
                        // 如果不是进度行，则添加到日志中
                        richTextBox1.AppendText(cleanText + "\r\n");
                        // 滚动到最底部
                        richTextBox1.SelectionStart = richTextBox1.Text.Length;
                        richTextBox1.ScrollToCaret();
                    }
                    */
                }));
            }
        }


        // 添加一个方法来过滤ANSI转义序列
        private string RemoveAnsiEscapeCodes(string input)
        {
            // 匹配ANSI转义序列的正则表达式
            // 这将匹配类似 "[2m"、"[0m"、"[92m" 等格式的ANSI颜色代码
            return System.Text.RegularExpressions.Regex.Replace(input, @"\x1B\[[0-9;]*[mGK]", string.Empty);
        }

        private void SubsCheckProcess_Exited(object sender, EventArgs e)
        {
            // 进程退出时，在 UI 线程上更新控件
            BeginInvoke(new Action(() =>
            {
                Log("subs-check.exe 已退出");
                button1.Text = "▶️ 启动";

                // 更新菜单项的启用状态
                startMenuItem.Enabled = true;
                stopMenuItem.Enabled = false;

                // 重新启用控件
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;
                numericUpDown4.Enabled = true;
                numericUpDown5.Enabled = true;
                numericUpDown6.Enabled = true;
                textBox1.Enabled = true;
                groupBox3.Enabled = true;
            }));
        }

        /// <summary>
        /// 获取本地局域网IP地址，如果有多个则让用户选择
        /// </summary>
        /// <returns>用户选择的IP地址，如果未选择则返回127.0.0.1</returns>
        private string GetLocalLANIP()
        {
            try
            {
                // 获取所有网卡的IP地址
                List<string> lanIPs = new List<string>();

                // 获取所有网络接口
                foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    // 排除loopback、虚拟网卡和非活动网卡
                    if (ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                        ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        // 获取该网卡的所有IP地址
                        foreach (System.Net.NetworkInformation.UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            // 只添加IPv4地址且不是回环地址
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                !System.Net.IPAddress.IsLoopback(ip.Address))
                            {
                                lanIPs.Add(ip.Address.ToString());
                            }
                        }
                    }
                }

                // 如果没有找到任何IP地址，返回本地回环地址
                if (lanIPs.Count == 0)
                {
                    return "127.0.0.1";
                }
                // 如果只找到一个IP地址，直接返回
                else if (lanIPs.Count == 1)
                {
                    return lanIPs[0];
                }
                // 如果有多个IP地址，让用户选择
                else
                {
                    // 创建选择窗口
                    Form selectForm = new Form();
                    selectForm.Text = "选择局域网IP地址";
                    selectForm.StartPosition = FormStartPosition.CenterParent;
                    /*
                    selectForm.Width = 520;  // 保持宽度
                    selectForm.Height = 320; // 增加高度以容纳额外的警告标签
                    selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    */
                    selectForm.AutoSize = true;  // 启用自动大小调整
                    selectForm.AutoSizeMode = AutoSizeMode.GrowAndShrink;  // 根据内容调整大小
                    selectForm.FormBorderStyle = FormBorderStyle.FixedSingle;  // 使用固定但可调整的边框
                    selectForm.ShowIcon = false;
                    selectForm.MaximizeBox = false;
                    selectForm.MinimizeBox = false;

                    // 添加说明标签
                    Label label = new Label();
                    label.Text = "发现多个局域网IP地址：\n\n" +
                                 "· 仅在本机访问：直接点击【取消】，将使用127.0.0.1\n\n" +
                                 "· 局域网内其他设备访问：请在下面列表中选择一个正确的局域网IP";
                    label.Location = new Point(15, 10);
                    label.AutoSize = true;
                    label.MaximumSize = new Size(380, 0); // 设置最大宽度，允许自动换行
                    selectForm.Controls.Add(label);

                    // 计算标签高度以正确放置列表框
                    int labelHeight = label.Height + 20;

                    // 添加IP地址列表框
                    ListBox listBox = new ListBox();
                    listBox.Location = new Point(15, labelHeight);
                    listBox.Width = 380;
                    listBox.Height = 130; // 保持列表框高度
                    foreach (string ip in lanIPs)
                    {
                        listBox.Items.Add(ip);
                    }
                    // 查找非".1"结尾的IP地址，如果所有IP都以".1"结尾，则使用第一个IP
                    int selectedIndex = 0;
                    for (int i = 0; i < lanIPs.Count; i++)
                    {
                        if (!lanIPs[i].EndsWith(".1"))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }

                    // 设置选中的索引
                    listBox.SelectedIndex = selectedIndex;
                    selectForm.Controls.Add(listBox);

                    // 添加警告标签（放在列表框下方）
                    Label warningLabel = new Label();
                    warningLabel.Text = "注意：选择错误的IP会导致局域网内其他设备无法访问。\n\n　　　推荐您可以先尝试使用非“.1”结尾的IP！";
                    warningLabel.Location = new Point(15, labelHeight + listBox.Height + 10);
                    warningLabel.AutoSize = true;
                    warningLabel.ForeColor = Color.Red; // 警告文本使用红色
                    selectForm.Controls.Add(warningLabel);

                    // 计算按钮位置（居中排布）
                    int buttonY = labelHeight + listBox.Height + warningLabel.Height + 20;
                    int buttonTotalWidth = 75 * 2 + 15; // 两个按钮的宽度加间距
                    int buttonStartX = (selectForm.ClientSize.Width - buttonTotalWidth) / 2;

                    // 添加确定按钮
                    Button okButton = new Button();
                    okButton.Text = "确定";
                    okButton.DialogResult = DialogResult.OK;
                    okButton.Location = new Point(buttonStartX, buttonY);
                    okButton.Width = 75;
                    selectForm.Controls.Add(okButton);
                    selectForm.AcceptButton = okButton;

                    // 添加取消按钮
                    Button cancelButton = new Button();
                    cancelButton.Text = "取消";
                    cancelButton.DialogResult = DialogResult.Cancel;
                    cancelButton.Location = new Point(buttonStartX + 90, buttonY);
                    cancelButton.Width = 75;
                    selectForm.Controls.Add(cancelButton);
                    selectForm.CancelButton = cancelButton;

                    // 显示选择窗口
                    if (selectForm.ShowDialog() == DialogResult.OK)
                    {
                        return listBox.SelectedItem.ToString();
                    }
                    else
                    {
                        return "127.0.0.1"; // 如果用户取消，返回本地回环地址
                    }
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取局域网IP地址时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "127.0.0.1";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string 本地IP = GetLocalLANIP();
            try
            {
                // 构造URL
                string url = comboBox4.Text == "Clash" ? $"http://{本地IP}:{numericUpDown7.Value}/api/file/mihomo" : $"http://{本地IP}:{numericUpDown7.Value}/download/sub";

                // 将URL复制到剪贴板
                Clipboard.SetText(url);
                button3.Text = "复制成功";
                timer2.Enabled = true;
                // 可选：显示提示消息
                //MessageBox.Show($"URL已复制到剪贴板：\n{url}", "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制到剪贴板时出错：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            button3.Text = "复制订阅";
        }

        private void comboBox3_Leave(object sender, EventArgs e)
        {
            // 检查是否有内容
            if (string.IsNullOrWhiteSpace(comboBox3.Text)) 
            {
                comboBox3.Text = "自动选择";
                return;
            }

            string input = comboBox3.Text.Trim();

            // 检查是否存在 "://" 协议部分
            int protocolIndex = input.IndexOf("://");
            if (protocolIndex >= 0)
            {
                // 保留 "://" 之后的内容
                input = input.Substring(protocolIndex + 3);
            }

            // 检查是否存在 "/" 路径部分
            int pathIndex = input.IndexOf('/');
            if (pathIndex >= 0)
            {
                // 只保留 "/" 之前的域名部分
                input = input.Substring(0, pathIndex);
            }

            // 更新 comboBox3 的文本
            comboBox3.Text = input;
        }

        private void 判断保存类型()
        {
            if (comboBox1.Text == "本地" || button2.Text == "高级设置∨")
            {
                groupBox4.Visible = false;
                groupBox5.Visible = false;
                groupBox6.Visible = false;
            }
            else if (comboBox1.Text == "gist" && button2.Text == "高级设置∧")
            {
                groupBox4.Visible = true;

                groupBox5.Visible = false;
                groupBox6.Visible = false;
            }
            else if (comboBox1.Text == "r2" && button2.Text == "高级设置∧")
            {
                groupBox5.Location = groupBox4.Location;
                groupBox5.Visible = true;

                groupBox4.Visible = false;
                groupBox6.Visible = false;
            }
            else if (comboBox1.Text == "webdav" && button2.Text == "高级设置∧")
            {
                groupBox6.Location = groupBox4.Location;
                groupBox6.Visible = true;

                groupBox4.Visible = false;
                groupBox5.Visible = false;
            }
        }

        private void comboBox1_TextChanged(object sender, EventArgs e)
        {
            判断保存类型();
            if(!(comboBox1.Text == "本地" || comboBox1.Text == "") && button2.Text == "高级设置∨") button2_Click(sender, e);
        }

        private void textBox3_Enter(object sender, EventArgs e)
        {
            textBox3.PasswordChar = '\0';
            textBox6.PasswordChar = '\0';
            textBox8.PasswordChar = '\0';
        }

        private void textBox3_Leave(object sender, EventArgs e)
        {
            textBox3.PasswordChar = '*';
            textBox6.PasswordChar = '*';
            textBox8.PasswordChar = '*';
        }

        private void textBox10_Enter(object sender, EventArgs e)
        {
            textBox10.PasswordChar = '\0';
            if (textBox10.Text == "请输入密钥")
            {
                textBox10.Text = "";
                textBox10.ForeColor = Color.Black;
            }
        }

        private void textBox10_Leave(object sender, EventArgs e)
        {
            
            if (textBox10.Text == "")
            {
                textBox10.PasswordChar = '\0';
                textBox10.Text = "请输入密钥";
                textBox10.ForeColor = Color.Gray;
            }
            else
            {
                textBox10.ForeColor = Color.Black;
                textBox10.PasswordChar = '*';
            }
        }

        private void textBox7_Leave(object sender, EventArgs e)
        {
            // 检查是否有内容
            if (string.IsNullOrWhiteSpace(textBox7.Text))
                return;

            string input = textBox7.Text.Trim();

            try
            {
                // 尝试解析为 URI
                Uri uri = new Uri(input);

                // 构建基础 URL (scheme + authority)
                string baseUrl = $"{uri.Scheme}://{uri.Authority}";

                // 更新 textBox7 的文本为基础 URL
                textBox7.Text = baseUrl;
            }
            catch (UriFormatException)
            {
                // 如果输入的不是有效 URI，尝试使用简单的字符串处理
                // 查找双斜杠后的第一个斜杠
                int schemeIndex = input.IndexOf("://");
                if (schemeIndex >= 0)
                {
                    int pathStartIndex = input.IndexOf('/', schemeIndex + 3);
                    if (pathStartIndex >= 0)
                    {
                        // 截取到路径开始之前
                        textBox7.Text = input.Substring(0, pathStartIndex);
                    }
                }
            }
        }

        private void Log(string message, bool isError = false)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logType = isError ? "ERR" : "INF";
            richTextBox1.AppendText($"{timestamp} {logType} {message}\r\n");

            if (richTextBox1.IsHandleCreated)
            {
                richTextBox1.BeginInvoke((MethodInvoker)(() =>
                {
                    // 滚动到最底部
                    richTextBox1.SelectionStart = richTextBox1.Text.Length;
                    richTextBox1.ScrollToCaret();
                }));
            }
        }

        private void 恢复窗口()
        {
            // 首先显示窗体
            this.Show();

            // 强制停止当前布局逻辑
            this.SuspendLayout();

            // 恢复窗口状态
            this.WindowState = FormWindowState.Normal;

            // 强制重新布局
            this.ResumeLayout(true); // 参数true表示立即执行布局

            // 调用刷新布局的方法
            this.PerformLayout();

            // 处理WindowsForms消息队列中的所有挂起消息
            Application.DoEvents();

            // 激活窗口（使其获得焦点）
            this.Activate();
        }

        private void 隐藏窗口()
        {
            // 隐藏窗体（从任务栏消失）
            this.Hide();

            // 确保通知图标可见
            notifyIcon1.Visible = true;

            // 可选：显示气泡提示
            notifyIcon1.ShowBalloonTip(1000, "SubsCheck", "程序已最小化到系统托盘", ToolTipIcon.Info);
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 检查窗口是否可见
                if (this.Visible)
                {
                    // 如果窗口当前可见，则隐藏窗口
                    隐藏窗口();
                }
                else
                {
                    // 如果窗口当前不可见，则恢复窗口
                    恢复窗口();
                }
            }
        }

        // 创建专用方法用于异步检测GitHub代理
        private async Task<string> DetectGitHubProxyAsync(List<string> proxyItems)
        {
            bool proxyFound = false;
            string detectedProxyURL = "";

            Log("检测可用 GitHub 代理...");

            // 遍历随机排序后的代理列表
            foreach (string proxyItem in proxyItems)
            {
                string checkUrl = $"https://{proxyItem}/https://raw.githubusercontent.com/cmliu/SubsCheck-Win-GUI/master/packages.config";
                Log($"正在测试 GitHub 代理: {proxyItem}");
                richTextBox1.Refresh();

                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(5); // 设置5秒超时
                                                                  // 添加User-Agent头，避免被拒绝访问
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win32; x86) AppleWebKit/537.36 (KHTML, like Gecko) cmliu/SubsCheck-Win-GUI");

                        // 使用异步方式
                        HttpResponseMessage response = await client.GetAsync(checkUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            // 找到可用代理
                            detectedProxyURL = $"https://{proxyItem}/";
                            Log($"找到可用 GitHub 代理: {proxyItem}");
                            proxyFound = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误但继续尝试下一个
                    Log($"代理 {proxyItem} 测试失败: {ex.Message}", true);
                    richTextBox1.Refresh();
                }
            }

            // 如果没有找到可用的代理
            if (!proxyFound)
            {
                Log("未找到可用的 GitHub 代理，请在高级设置中手动设置。", true);
                MessageBox.Show("未找到可用的 GitHub 代理。\n\n请打开高级设置手动填入一个可用的Github Proxy，或检查您的网络连接。",
                    "代理检测失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return detectedProxyURL;
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            try
            {
                button5.Enabled = false;
                button1.Enabled = false;
                // 清空日志
                richTextBox1.Clear();
                Log("开始检查和下载最新版本的 subs-check.exe...");

                // 获取当前应用程序目录
                string executablePath = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                string subsCheckPath = Path.Combine(executablePath, "subs-check.exe");

                // 检查文件是否存在
                if (File.Exists(subsCheckPath))
                {
                    Log($"发现 subs-check.exe，正在删除...");

                    try
                    {
                        // 首先检查是否有进程正在运行
                        Process[] processes = Process.GetProcessesByName("subs-check");
                        if (processes.Length > 0)
                        {
                            Log("发现正在运行的 subs-check.exe 进程，正在强制结束...");
                            foreach (Process process in processes)
                            {
                                try
                                {
                                    process.Kill();
                                    process.WaitForExit();
                                    Log($"成功结束 subs-check.exe 进程(ID: {process.Id})");
                                }
                                catch (Exception ex)
                                {
                                    Log($"结束进程时出错(ID: {process.Id}): {ex.Message}", true);
                                }
                            }
                        }

                        // 删除文件
                        File.Delete(subsCheckPath);
                        Log("成功删除旧版本 subs-check.exe");
                    }
                    catch (Exception ex)
                    {
                        Log($"删除 subs-check.exe 时出错: {ex.Message}", true);
                        MessageBox.Show($"无法删除现有的 subs-check.exe 文件: {ex.Message}\n\n请手动删除后重试，或者检查文件是否被其他程序占用。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        button5.Enabled = true;
                        return;
                    }
                }
                else
                {
                    Log("未找到现有的 subs-check.exe 文件，将直接下载最新版本");
                }

                // 检测可用的 GitHub 代理
                if (!string.IsNullOrEmpty(comboBox3.Text) && comboBox3.Text == "自动选择")
                {
                    // 创建不包含"自动选择"的代理列表
                    List<string> proxyItems = new List<string>();
                    for (int j = 0; j < comboBox3.Items.Count; j++)
                    {
                        string proxyItem = comboBox3.Items[j].ToString();
                        if (proxyItem != "自动选择")
                            proxyItems.Add(proxyItem);
                    }

                    // 随机打乱列表顺序
                    Random random = new Random();
                    proxyItems = proxyItems.OrderBy(x => random.Next()).ToList();

                    // 异步检测可用代理
                    githubProxyURL = await DetectGitHubProxyAsync(proxyItems);

                    // 如果未能找到可用代理，提示用户
                    if (string.IsNullOrEmpty(githubProxyURL))
                    {
                        Log("未能找到可用的 GitHub 代理，下载可能会失败", true);
                    }
                }
                else if (!string.IsNullOrEmpty(comboBox3.Text))
                {
                    githubProxyURL = $"https://{comboBox3.Text}/";
                    Log($"使用指定的 GitHub 代理: {comboBox3.Text}");
                }
                else
                {
                    Log("未设置 GitHub 代理，将尝试直接下载", true);
                }

                // 下载最新版本的 subs-check.exe
                await DownloadSubsCheckEXE();

                // 完成
                Log("内核更新完成！");
            }
            catch (Exception ex)
            {
                Log($"操作过程中出错: {ex.Message}", true);
                MessageBox.Show($"处理过程中出现错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                button5.Enabled = true;
                button1.Enabled = true;
            }
        }

        private decimal 订阅端口;
        private decimal SubStore端口;
        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {
            // 检查numericUpDown7是否存在并且与numericUpDown6的值相等
            if (numericUpDown6.Value == numericUpDown7.Value)
            {
                // 显示警告消息
                MessageBox.Show("订阅端口 和 Sub-Store端口 不能相同！",
                               "端口冲突",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Warning);

                // 将numericUpDown6的值恢复为更改前的值
                numericUpDown6.Value = 订阅端口;
                numericUpDown7.Value = SubStore端口;
            }
            else
            {
                // 保存当前值作为下次比较的基准
                订阅端口 = numericUpDown6.Value;
                SubStore端口 = numericUpDown7.Value;
            }
        }

        /// <summary>
        /// 异步检测并强制终止所有程序目录下的output\node.exe进程
        /// </summary>
        private async Task KillNodeProcessAsync()
        {
            try
            {
                Log("检查 node.exe 进程状态...");

                // 获取当前应用程序的执行目录
                string executablePath = Path.GetDirectoryName(Application.ExecutablePath);
                string nodeExePath = Path.Combine(executablePath, "output", "node.exe");

                // 获取所有node.exe进程
                Process[] nodeProcesses = Process.GetProcessesByName("node");

                if (nodeProcesses.Length == 0)
                {
                    Log("未发现运行中的 node.exe 进程");
                    return;
                }

                Log($"发现 {nodeProcesses.Length} 个 node.exe 进程，开始检查并终止匹配路径的进程...");

                int terminatedCount = 0;

                foreach (Process process in nodeProcesses)
                {
                    try
                    {
                        // 使用Task.Run将可能耗时的操作放在后台线程执行
                        string processPath = await Task.Run(() => {
                            try
                            {
                                return process.MainModule?.FileName;
                            }
                            catch (Exception)
                            {
                                return null;
                            }
                        });

                        // 检查是否匹配我们要查找的node.exe路径
                        if (!string.IsNullOrEmpty(processPath) &&
                            processPath.Equals(nodeExePath, StringComparison.OrdinalIgnoreCase))
                        {
                            // 找到匹配的进程，终止它
                            Log($"发现匹配路径的 node.exe 进程(ID: {process.Id})，正在强制结束...");

                            await Task.Run(() => {
                                process.Kill();
                                process.WaitForExit();
                            });

                            Log($"成功结束 node.exe 进程(ID: {process.Id})");
                            terminatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 访问进程信息时可能会因为权限问题抛出异常
                        Log($"访问或终止进程(ID: {process.Id})时出错: {ex.Message}", true);
                    }
                }

                if (terminatedCount > 0)
                {
                    Log($"总共终止了 {terminatedCount} 个匹配路径的 node.exe 进程");
                }
                else
                {
                    Log("未发现需要终止的 node.exe 进程");
                }
            }
            catch (Exception ex)
            {
                Log($"检查或终止 node.exe 进程时出错: {ex.Message}", true);
            }
        }

        private async void textBox1_DoubleClick(object sender, EventArgs e)
        {
            if (textBox1.Enabled)
            {
                // 创建EditURLs窗口的实例
                EditURLs editURLsForm = new EditURLs();

                // 传递当前textBox1的内容到EditURLs窗口
                editURLsForm.UrlContent = textBox1.Text + "\n";
                editURLsForm.githubProxys = comboBox3.Items;
                editURLsForm.githubProxy = comboBox3.Text;
                // 显示对话框并等待结果
                DialogResult result = editURLsForm.ShowDialog();

                // 如果用户点击了"保存并关闭"按钮（返回DialogResult.OK）
                if (result == DialogResult.OK)
                {
                    // 获取编辑后的内容，按行拆分，过滤空行
                    string[] lines = editURLsForm.UrlContent.Split(
                        new[] { "\r\n", "\r", "\n" },
                        StringSplitOptions.RemoveEmptyEntries);

                    // 去除每行首尾的空白字符
                    for (int i = 0; i < lines.Length; i++)
                    {
                        lines[i] = lines[i].Trim();
                    }

                    // 再次过滤掉空行
                    lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

                    // 将处理后的内容更新到Form1的textBox1
                    textBox1.Text = string.Join(Environment.NewLine, lines);
                    await SaveConfig(false);
                    Log("已保存订阅地址列表。");
                }
            }

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false) checkBox2.Checked = false;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked == true) checkBox1.Checked = true;
        }

        private void checkBoxAutoStart_CheckedChanged(object sender, EventArgs e)
        {
         if (checkBoxAutoStart.Checked)
            {
            Log("已启用实时启动功能，程序启动时将自动开始检查节点");

            // 只有当按钮显示"启动"时才实时启动
                 if (button1.Text == "▶️ 启动")
                 {
                  button1_Click(this, EventArgs.Empty); // 模拟点击启动按钮
                 }
             }
             else
             {
            Log("已禁用实时启动功能");
            }

             // 立即保存设置
            SaveConfig(false); // 不检查github代理
            
        }

        private async void timer3_Tick(object sender, EventArgs e)
        {
            if (button1.Text == "⏹️ 停止") 
            {
                Log("subs-check.exe 运行时满24小时，自动重启清理内存占用。");
                // 停止 subs-check.exe 程序
                StopSubsCheckProcess();
                // 结束 Sub-Store
                await KillNodeProcessAsync();
                // 重新启动 subs-check.exe 程序
                StartSubsCheckProcess();
                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;
                numericUpDown3.Enabled = false;
                numericUpDown4.Enabled = false;
                numericUpDown5.Enabled = false;
                numericUpDown6.Enabled = false;
                numericUpDown7.Enabled = false;
                comboBox1.Enabled = false;
                textBox1.Enabled = false;
                groupBox3.Enabled = false;
                groupBox4.Enabled = false;
                groupBox5.Enabled = false;
                groupBox6.Enabled = false;
                button1.Text = "⏹️ 停止";
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // 创建 CheckUpdates 窗口实例
            CheckUpdates checkUpdatesForm = new CheckUpdates();

            // 传递必要的数据和状态
            checkUpdatesForm.githubProxys = comboBox3.Items;
            checkUpdatesForm.githubProxy = comboBox3.Text;

            checkUpdatesForm.当前subsCheck版本号 = 当前subsCheck版本号;
            checkUpdatesForm.当前GUI版本号 = 当前GUI版本号;
            checkUpdatesForm.最新GUI版本号 = 最新GUI版本号;

            // 为 CheckUpdates 的 button2 添加点击事件处理程序
            checkUpdatesForm.FormClosed += (s, args) => {
                // 移除事件处理，避免内存泄漏
                if (checkUpdatesForm.DialogResult == DialogResult.OK)
                {
                    // 如果返回OK结果，表示按钮被点击并需要更新内核
                    button5_Click(this, EventArgs.Empty);
                }
            };

            // 设置 button2 点击后关闭窗口并返回 DialogResult.OK
            // 这需要在 CheckUpdates.cs 中修改 button2_Click 方法

            // 显示 CheckUpdates 窗口
            checkUpdatesForm.ShowDialog();
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked) numericUpDown8.Enabled = true;
            else numericUpDown8.Enabled = false;
        }

        private async void comboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox5.Text.Contains("[内置]")) await ProcessComboBox5Selection(true);
        }

        private async Task ProcessComboBox5Selection(bool 汇报Log = false)
        {
            // 确定文件名和下载URL
            string fileName;
            string downloadFilePath;
            string downloadUrl;
            string displayName;
            string executablePath = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            if (comboBox5.Text.Contains("[内置]布丁狗"))
            {
                fileName = "bdg.yaml";
                displayName = "[内置]布丁狗的订阅转换";
                downloadUrl = "https://raw.githubusercontent.com/cmliu/ACL4SSR/main/yaml/bdg.yaml";
            }
            else // [内置]ACL4SSR
            {
                fileName = "ACL4SSR_Online_Full.yaml";
                displayName = "[内置]ACL4SSR_Online_Full";
                downloadUrl = "https://raw.githubusercontent.com/beck-8/override-hub/main/yaml/ACL4SSR_Online_Full.yaml";
            }

            // 确保output文件夹存在
            string outputFolderPath = Path.Combine(executablePath, "output");
            if (!Directory.Exists(outputFolderPath))
            {
                Directory.CreateDirectory(outputFolderPath);
            }

            // 确定文件完整路径
            downloadFilePath = Path.Combine(outputFolderPath, fileName);

            // 检查文件是否存在
            if (!File.Exists(downloadFilePath))
            {
                Log($"{displayName} 覆写配置文件 未找到，正在下载...");

                // 重置进度条
                progressBar1.Value = 0;

                // 添加GitHub代理前缀如果有
                string fullDownloadUrl = githubProxyURL + downloadUrl;

                try
                {
                    // 创建不使用系统代理的HttpClientHandler
                    using (HttpClientHandler handler = new HttpClientHandler { UseProxy = false, Proxy = null })
                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win32; x86) AppleWebKit/537.36 (KHTML, like Gecko) cmliu/SubsCheck-Win-GUI");
                        client.Timeout = TimeSpan.FromSeconds(15); // 设置15秒超时

                        // 先获取文件大小
                        HttpResponseMessage headResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, fullDownloadUrl));
                        long totalBytes = headResponse.Content.Headers.ContentLength ?? 0;

                        // 如果无法获取文件大小，显示不确定进度
                        if (totalBytes == 0)
                        {
                            //Log($"无法获取 {displayName} 文件大小，将显示不确定进度");
                        }

                        // 创建下载请求并获取响应流
                        using (var response = await client.GetAsync(fullDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                using (var contentStream = await response.Content.ReadAsStreamAsync())
                                using (var fileStream = new FileStream(downloadFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                                {
                                    byte[] buffer = new byte[8192];
                                    long totalBytesRead = 0;
                                    int bytesRead;

                                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                    {
                                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;

                                        // 更新进度条
                                        if (totalBytes > 0)
                                        {
                                            int progressPercentage = (int)((totalBytesRead * 100) / totalBytes);
                                            // 确保进度值在有效范围内 (0-100)
                                            progressPercentage = Math.Min(100, Math.Max(0, progressPercentage));
                                            progressBar1.Value = progressPercentage;
                                        }
                                    }

                                    // 确保进度条显示100%
                                    progressBar1.Value = 100;
                                }

                                Log($"{displayName} 覆写配置文件 下载成功");
                            }
                            else
                            {
                                Log($"{displayName} 覆写配置文件 下载失败: HTTP {(int)response.StatusCode} {response.ReasonPhrase}", true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"{displayName} 覆写配置文件 下载失败: {ex.Message}", true);
                    // 出错时重置进度条
                    progressBar1.Value = 0;
                }
            }
            else
            {
                if (汇报Log) Log($"{displayName} 覆写配置文件 已就绪。");
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown1.Value > 128)
            {
                string warningMessage =
                    "⚠️ 高并发风险提醒 ⚠️\n\n" +
                    "您设置的并发数值过高，可能导致：\n\n" +
                    "• 运营商判定为异常流量并限制网络\n" +
                    "• 路由器性能压力过大\n" +
                    "• 测速结果不准确\n\n" +
                    "并发数设置建议：\n" +
                    "• 宽带峰值/50Mbps：一般对网络无影响\n" +
                    "• 宽带峰值/25Mbps：可能会影响同网络下载任务\n" +
                    "• 宽带峰值/10Mbps：可能会影响同网络下其他设备的上网体验";

                MessageBox.Show(warningMessage, "网络安全警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if(checkBox4.Checked) textBox10.Enabled = true;
            else textBox10.Enabled = false;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            string 本地IP = GetLocalLANIP();
            try
            {
                // 构造URL
                string url = $"http://{本地IP}:{numericUpDown6.Value}/admin";

                // 使用系统默认浏览器打开URL
                System.Diagnostics.Process.Start(url);

                Log($"正在浏览器中打开 Sub-Store 管理页面: {url}");
            }
            catch (Exception ex)
            {
                Log($"打开浏览器失败: {ex.Message}", true);
                MessageBox.Show($"打开浏览器时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 获取API状态信息并返回包含6个元素的字符串数组
        /// </summary>
        /// <returns>
        /// 包含6个元素的字符串数组：
        /// [0] - 状态类型 ("checking"/"idle"/"error")
        /// [1] - 状态图标类别 ("primary"/"success"/"danger")
        /// [2] - 状态文本 ("正在检测中..."/"空闲"/"获取状态失败")
        /// [3] - 节点总数 (proxyCount或"N/A")
        /// [4] - 进度百分比 (progress或"N/A")
        /// [5] - 可用节点数量 (available或"N/A")
        /// </returns>
        private async Task<string[]> GetApiStatusAsync()
        {
            string[] resultArray = new string[6];
            string baseUrl = $"http://127.0.0.1:{numericUpDown6.Value}";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 设置基础URL
                    client.BaseAddress = new Uri(baseUrl);

                    // 添加API密钥请求头
                    client.DefaultRequestHeaders.Add("X-API-Key", WebUIapiKey);

                    // 设置超时时间
                    client.Timeout = TimeSpan.FromSeconds(5);

                    // 发送请求
                    HttpResponseMessage response = await client.GetAsync("/api/status");

                    // 检查响应状态
                    if (response.IsSuccessStatusCode)
                    {
                        // 读取响应内容
                        string content = await response.Content.ReadAsStringAsync();

                        // 解析JSON
                        JObject data = JObject.Parse(content);

                        if (data["checking"] != null && data["checking"].Value<bool>())
                        {
                            // 正在检测状态
                            resultArray[0] = "checking";
                            resultArray[1] = "primary";
                            resultArray[2] = "正在检测中...";

                            // 提取节点数据
                            resultArray[3] = data["proxyCount"]?.ToString() ?? "0";
                            resultArray[4] = data["progress"]?.ToString() ?? "0";
                            resultArray[5] = data["available"]?.ToString() ?? "0";
                        }
                        else
                        {
                            // 空闲状态
                            resultArray[0] = "idle";
                            resultArray[1] = "success";
                            resultArray[2] = "空闲";

                            // 空闲时相关数据设为N/A
                            resultArray[3] = "N/A";
                            resultArray[4] = "N/A";
                            resultArray[5] = "N/A";
                        }
                    }
                    else
                    {
                        // 请求失败，例如未授权
                        resultArray[0] = "error";
                        resultArray[1] = "danger";
                        resultArray[2] = $"API请求失败: {(int)response.StatusCode}";
                        resultArray[3] = "N/A";
                        resultArray[4] = "N/A";
                        resultArray[5] = "N/A";
                    }
                }
            }
            catch (Exception ex)
            {
                // 发生异常
                resultArray[0] = "error";
                resultArray[1] = "danger";
                resultArray[2] = $"获取状态失败: {ex.Message}";
                resultArray[3] = "N/A";
                resultArray[4] = "N/A";
                resultArray[5] = "N/A";

                // 可选：记录错误到日志
                Log($"获取API状态失败: {ex.Message}", true);
            }

            return resultArray;
        }

        private async void timer4_Tick(object sender, EventArgs e)
        {
            //if (!button7.Enabled) button7.Enabled = true;
            string[] subscheck状态 = await GetApiStatusAsync();
            string 状态类型 = subscheck状态[0];
            string 状态图标类别 = subscheck状态[1];
            string 状态文本 = subscheck状态[2];
            string 节点总数 = subscheck状态[3];
            string 进度百分比 = subscheck状态[4];
            string 可用节点数量 = subscheck状态[5];
            // 更新状态文本

            if (状态类型 == "checking")
            {
                button7.Text = "⏸️ 暂停";
                nodeInfo = $"({进度百分比}/{节点总数}) 可用: {可用节点数量}";
                int nodeTotal = int.Parse(节点总数);
                if (nodeTotal > 0) {
                    int 进度条百分比 = int.Parse(进度百分比) * 100 / nodeTotal;
                    progressBar1.Value = 进度条百分比;
                    if (!button7.Enabled) button7.Enabled = true;
                }
                
                // 确保通知图标文本不超过63个字符
                string notifyText = "SubsCheck: " + nodeInfo;
                if (notifyText.Length > 63)
                {
                    notifyText = notifyText.Substring(0, 60) + "...";
                }
                notifyIcon1.Text = notifyText;
                textBox1.Enabled = false;
            }
            else if (状态类型 == "idle")
            {
                button7.Text = "⏯️ 开始";
                progressBar1.Value = 100;
                nodeInfo = $"等待{nextCheckTime}";
                notifyIcon1.Text = "SubsCheck: 已就绪\n" + nextCheckTime; ;
                textBox1.Enabled = true;
            }
            else if (状态类型 == "error")
            {
                button7.Text = "🔀 未知";
                nodeInfo = 状态文本;
            }
            groupBox2.Text = $"实时日志 {nodeInfo}";
        }

        private async void button7_Click(object sender, EventArgs e)
        {
            button7.Enabled = false;
            timer4.Enabled = false;

            try
            {
                bool isSuccess;

                if (button7.Text == "⏯️ 开始")
                {
                    isSuccess = await SendApiRequestAsync("/api/trigger-check", "节点检查");
                    if (isSuccess)
                    {
                        button7.Text = "⏸️ 暂停";
                        textBox1.Enabled = false; // 检查开始后禁用订阅编辑
                    }
                }
                else // "⏸️ 暂停"
                {
                    isSuccess = await SendApiRequestAsync("/api/force-close", "强制关闭");
                }

                // 如果请求失败，更新按钮状态为未知
                if (!isSuccess) button7.Text = "🔀 未知";
            }
            finally
            {
                // 无论成功失败都重新启用定时器和按钮
                timer4.Enabled = true;
                timer4.Start();
                //button7.Enabled = true;
            }
        }

        /// <summary>
        /// 发送API请求到SubsCheck服务
        /// </summary>
        /// <param name="endpoint">API端点路径</param>
        /// <param name="operationName">操作名称(用于日志)</param>
        /// <returns>操作是否成功</returns>
        private async Task<bool> SendApiRequestAsync(string endpoint, string operationName)
        {
            try
            {
                // 获取API基础地址和API密钥
                string baseUrl = $"http://127.0.0.1:{numericUpDown6.Value}";

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(baseUrl);
                    client.DefaultRequestHeaders.Add("X-API-Key", WebUIapiKey);
                    client.Timeout = TimeSpan.FromSeconds(10);

                    // 发送POST请求
                    HttpResponseMessage response = await client.PostAsync(endpoint, new StringContent(""));

                    // 检查响应状态
                    if (response.IsSuccessStatusCode)
                    {
                        Log($"成功{operationName}");
                        return true;
                    }
                    else
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Log($"{operationName}失败: HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{errorContent}", true);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"{operationName}时发生错误: {ex.Message}", true);
                return false;
            }
        }

        private void textBox11_Leave(object sender, EventArgs e)
        {
            if (IsValidCronExpression(textBox11.Text))
            {
                // 计算并显示cron表达式的说明
                string cronDescription = GetCronExpressionDescription(textBox11.Text);
                // 可以用工具提示或者消息框显示，这里使用消息框
                //MessageBox.Show(cronDescription, "Cron表达式说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Log($"Cron表达式说明 {cronDescription}");
            }
            else
            {
                MessageBox.Show("请输入有效的cron表达式，例如：*/30 * * * *", "无效的cron表达式",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox11.Focus();
                textBox11.Text = "0 */2 * * *"; // 恢复默认值
            }
        }

        /// <summary>
        /// 验证输入文本是否是合法的cron表达式
        /// </summary>
        /// <returns>如果是合法的cron表达式，则返回true；否则返回false</returns>
        private bool IsValidCronExpression(string cron表达式)
        {
            string cronExpression = cron表达式.Trim();

            // 如果是空字符串，则不是有效表达式
            if (string.IsNullOrWhiteSpace(cronExpression))
                return false;

            // 分割cron表达式为5个部分：分钟 小时 日期 月份 星期
            string[] parts = cronExpression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // cron表达式必须有5个部分
            if (parts.Length != 5)
                return false;

            try
            {
                // 验证每个部分
                // 分钟 (0-59)
                if (!IsValidCronField(parts[0], 0, 59))
                    return false;

                // 小时 (0-23)
                if (!IsValidCronField(parts[1], 0, 23))
                    return false;

                // 日期 (1-31)
                if (!IsValidCronField(parts[2], 1, 31))
                    return false;

                // 月份 (1-12)
                if (!IsValidCronField(parts[3], 1, 12))
                    return false;

                // 星期 (0-7，0和7都表示星期日)
                if (!IsValidCronField(parts[4], 0, 7))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 验证cron表达式中的单个字段是否合法
        /// </summary>
        /// <param name="field">字段值</param>
        /// <param name="min">最小允许值</param>
        /// <param name="max">最大允许值</param>
        /// <returns>如果字段合法，则返回true；否则返回false</returns>
        private bool IsValidCronField(string field, int min, int max)
        {
            // 处理通配符 "*"
            if (field == "*")
                return true;

            // 处理步长 "*/n"
            if (field.StartsWith("*/"))
            {
                string stepStr = field.Substring(2);
                if (int.TryParse(stepStr, out int step))
                    return step > 0 && step <= max;
                return false;
            }

            // 处理范围 "n-m"
            if (field.Contains("-"))
            {
                string[] range = field.Split('-');
                if (range.Length != 2)
                    return false;

                if (int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                    return start >= min && end <= max && start <= end;
                return false;
            }

            // 处理列表 "n,m,k"
            if (field.Contains(","))
            {
                string[] values = field.Split(',');
                foreach (string item in values)
                {
                    if (!int.TryParse(item, out int itemValue) || itemValue < min || itemValue > max)
                        return false;
                }
                return true;
            }

            // 处理单个数字
            if (int.TryParse(field, out int fieldValue))
                return fieldValue >= min && fieldValue <= max;

            return false;
        }

        /// <summary>
        /// 获取cron表达式的友好文本说明
        /// </summary>
        /// <param name="cron表达式">要解析的cron表达式</param>
        /// <returns>返回cron表达式的执行时间说明</returns>
        private string GetCronExpressionDescription(string cron表达式)
        {
            try
            {
                string cronExpression = cron表达式.Trim();
                string[] parts = cronExpression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 5)
                    return "无效的cron表达式";

                // 分别解析每个部分
                string minuteDesc = ParseCronPart(parts[0], "分钟", 0, 59);
                string hourDesc = ParseCronPart(parts[1], "小时", 0, 23);
                string dayDesc = ParseCronPart(parts[2], "日", 1, 31);
                string monthDesc = ParseCronPart(parts[3], "月", 1, 12);
                string weekDesc = ParseCronPart(parts[4], "星期", 0, 7, true);

                // 组合最终说明
                string description = "执行时间: ";

                // 月份
                if (monthDesc != "每月")
                    description += monthDesc + "的";

                // 星期与日期的关系
                if (parts[2] == "*" && parts[4] != "*")
                    description += weekDesc + "的";
                else if (parts[2] != "*" && parts[4] == "*")
                    description += dayDesc;
                else if (parts[2] != "*" && parts[4] != "*")
                    description += $"{dayDesc}或{weekDesc}";
                else
                    description += "每天";

                // 时间（小时:分钟）
                description += $"{hourDesc}{minuteDesc}";

                return description;
            }
            catch
            {
                return "无法解析cron表达式";
            }
        }

        /// <summary>
        /// 解析cron表达式的单个部分
        /// </summary>
        private string ParseCronPart(string part, string unit, int min, int max, bool isWeekday = false)
        {
            // 处理星号，表示每个时间单位
            if (part == "*")
            {
                return $"每{unit}";
            }

            // 处理步长 */n
            if (part.StartsWith("*/"))
            {
                int step = int.Parse(part.Substring(2));
                return $"每{step}{unit}";
            }

            // 处理范围 n-m
            if (part.Contains("-"))
            {
                string[] range = part.Split('-');
                int start = int.Parse(range[0]);
                int end = int.Parse(range[1]);

                if (isWeekday)
                {
                    string[] weekdays = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日" };
                    return $"从{weekdays[start]}到{weekdays[end]}";
                }

                return $"从{start}{unit}到{end}{unit}";
            }

            // 处理列表 n,m,k
            if (part.Contains(","))
            {
                string[] values = part.Split(',');
                if (isWeekday)
                {
                    string[] weekdays = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日" };
                    return string.Join("、", values.Select(v => weekdays[int.Parse(v)]));
                }
                return $"{string.Join("、", values)}{unit}";
            }

            // 处理单个数字
            int value = int.Parse(part);
            if (isWeekday)
            {
                string[] weekdays = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日" };
                return weekdays[value];
            }
            return $"{value}{unit}";
        }

        private void 切换cron表达式(object sender, EventArgs e)
        {
            if (textBox11.Visible)
            {
                textBox11.Visible = false;
                label2.Visible = true;
                numericUpDown2.Visible = true;
                Log("下次检查时间间隔 使用分钟倒计时");
            }
            else
            {
                textBox11.Location = new Point(9, 48);
                textBox11.Visible = true;
                label2.Visible = false;
                numericUpDown2.Visible = false;
                Log("下次检查时间间隔 使用cron表达式");
            }
        }

        /// <summary>
        /// 获取计算机名的MD5哈希值
        /// </summary>
        /// <returns>返回计算机名的MD5哈希字符串(32位小写)</returns>
        private string GetComputerNameMD5()
        {
            try
            {
                // 获取计算机名
                string computerName = System.Environment.MachineName;

                // 引入必要的命名空间
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    // 将计算机名转换为字节数组
                    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(computerName);

                    // 计算MD5哈希值
                    byte[] hashBytes = md5.ComputeHash(inputBytes);

                    // 将字节数组转换为十六进制字符串
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        sb.Append(hashBytes[i].ToString("x2"));
                    }

                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Log($"计算计算机名MD5时出错: {ex.Message}", true);
                return "CMLiussss";
            }
        }

        // 添加辅助下载方法
        async Task<bool> DownloadFileAsync(HttpClient httpClient, string url, string filePath)
        {
            try
            {
                // 获取文件大小
                HttpResponseMessage headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                headResponse.EnsureSuccessStatusCode(); // 确保请求成功
                long totalBytes = headResponse.Content.Headers.ContentLength ?? 0;

                // 下载文件
                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode(); // 确保请求成功

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        byte[] buffer = new byte[8192];
                        long totalBytesRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            // 更新进度条
                            if (totalBytes > 0)
                            {
                                int progressPercentage = (int)((totalBytesRead * 100) / totalBytes);
                                progressPercentage = Math.Min(100, Math.Max(0, progressPercentage));
                                progressBar1.Value = progressPercentage;
                            }
                        }
                    }
                }

                return true; // 下载成功
            }
            catch
            {
                throw; // 重新抛出异常，让调用者处理
            }
        }

        private static about aboutWindow = null;
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // 检查窗口是否已经打开
            if (aboutWindow != null && !aboutWindow.IsDisposed)
            {
                // 窗口已经存在，激活它
                aboutWindow.Activate();
                return;
            }

            // 需要创建新窗口
            this.BeginInvoke(new Action(() =>
            {
                // 创建about窗口实例
                aboutWindow = new about();

                // 传递版本号信息
                aboutWindow.GuiVersion = 当前GUI版本号;
                aboutWindow.CoreVersion = 当前subsCheck版本号;

                // 添加窗口关闭时的处理，清除静态引用
                aboutWindow.FormClosed += (s, args) => aboutWindow = null;

                // 非模态显示窗口
                aboutWindow.Show(this);

                // 设置TopMost确保窗口显示在最前面
                aboutWindow.TopMost = true;
            }));
        }

        private void button8_Click(object sender, EventArgs e)
        {
            try
            {
                // 创建MoreYAML窗口实例
                MoreYAML moreYamlWindow = new MoreYAML();

                // 显示为模态对话框，这会阻塞主线程直到窗口关闭
                DialogResult result = moreYamlWindow.ShowDialog(this);

                // 如果需要，可以处理对话框的返回结果
                if (result == DialogResult.OK)
                {
                    // 用户点击了"确定"或某种完成操作的按钮
                    Log("补充参数配置已成功保存到 more.yaml 文件！设置已应用");
                }
            }
            catch (Exception ex)
            {
                Log($"打开MoreYAML窗口时出错: {ex.Message}", true);
                MessageBox.Show($"打开MoreYAML窗口时出错: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            checkBox5.Enabled = false;
            try
            {
                // 获取当前应用程序的可执行文件路径
                string appPath = Application.ExecutablePath;
                // 获取应用程序名称（不包含扩展名）
                string appName = Path.GetFileNameWithoutExtension(appPath);
                // 获取启动文件夹的路径
                string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                // 快捷方式文件的完整路径
                string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

                if (checkBox5.Checked)
                {
                    // 检查启动文件夹中是否已存在该快捷方式
                    if (File.Exists(shortcutPath))
                    {
                        Log("开机启动项已存在，无需重复创建");
                    }
                    else
                    {
                        // 创建快捷方式
                        CreateShortcut(appPath, shortcutPath, "-auto");
                        Log("已成功创建开机启动项，下次电脑启动时将自动运行程序");
                    }
                }
                else
                {
                    // 删除启动项
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                        Log("已移除开机启动项，下次开机将不会自动运行程序");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"设置开机启动项时出错: {ex.Message}", true);
                MessageBox.Show($"设置开机启动项失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 恢复CheckBox状态，避免UI状态与实际状态不一致
                checkBox5.CheckedChanged -= checkBox5_CheckedChanged;
                checkBox5.Checked = !checkBox5.Checked;
                checkBox5.CheckedChanged += checkBox5_CheckedChanged;
            }
            checkBox5.Enabled = true;
            await SaveConfig(false);
        }

        /// <summary>
        /// 创建指向指定路径应用程序的快捷方式
        /// </summary>
        /// <param name="targetPath">目标应用程序的完整路径</param>
        /// <param name="shortcutPath">要创建的快捷方式的完整路径</param>
        /// <param name="arguments">可选的启动参数</param>
        private void CreateShortcut(string targetPath, string shortcutPath, string arguments = "")
        {
            // 使用COM接口创建快捷方式
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(t);
            var shortcut = shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = targetPath;
            if (!string.IsNullOrEmpty(arguments))
                shortcut.Arguments = arguments; // 设置启动参数

            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.WindowStyle = 7; // 最小化启动: 7, 正常启动: 1, 最大化启动: 3
            shortcut.Description = "SubsCheck Win GUI自启动快捷方式";
            shortcut.IconLocation = targetPath + ",0"; // 使用应用程序自身的图标

            // 保存快捷方式
            shortcut.Save();

            // 释放COM对象
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }

        /// <summary>
        /// 检查启动参数中是否包含指定的参数
        /// </summary>
        /// <param name="parameterName">要检查的参数名称，例如"-autoup"</param>
        /// <returns>如果存在指定参数，则返回true；否则返回false</returns>
        private bool CheckCommandLineParameter(string parameterName)
        {
            // 获取命令行参数数组
            string[] args = Environment.GetCommandLineArgs();

            // 遍历所有参数，检查是否有匹配的参数
            foreach (string arg in args)
            {
                // 不区分大小写比较
                if (string.Equals(arg, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void richTextBox1_DoubleClick(object sender, EventArgs e)
        {
            // 检查是否有日志内容
            if (richTextBox1.TextLength > 0)
            {
                // 显示确认对话框，询问用户是否要清空日志
                DialogResult result = MessageBox.Show(
                    "是否要清空当前日志？",
                    "清空日志确认",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2); // 默认选择"否"按钮

                if (result == DialogResult.Yes)
                {
                    // 清空richTextBox1内容
                    richTextBox1.Clear();
                    // 记录一条清空日志的操作信息
                    Log("日志已清空");
                }
            }
        }
    }
}
