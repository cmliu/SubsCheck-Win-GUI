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
using YamlDotNet.Core;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

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
        public Form1()
        {
            InitializeComponent();
            originalNotifyIcon = notifyIcon1.Icon;

            toolTip1.SetToolTip(numericUpDown1, "并发线程数：推荐 宽带峰值/50M。");
            toolTip1.SetToolTip(numericUpDown2, "检查间隔时间(分钟)：放置后台的时候，下次自动测速的间隔时间。");
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
            // 设置通知图标的上下文菜单
            SetupNotifyIconContextMenu();
        }

        private void SetupNotifyIconContextMenu()
        {
            // 创建上下文菜单
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // 创建"启动"菜单项
            startMenuItem = new ToolStripMenuItem("启动");
            startMenuItem.Click += (sender, e) =>
            {
                if (button1.Text == "启动")
                {
                    button1_Click(sender, e);
                }
            };

            // 创建"停止"菜单项
            stopMenuItem = new ToolStripMenuItem("停止");
            stopMenuItem.Click += (sender, e) =>
            {
                if (button1.Text == "停止")
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
            this.Text = 标题 + " TG:CMLiussss BY:CM喂饭 干货满满";
            comboBox1.Text = "本地";
            comboBox4.Text = "通用订阅";
            ReadConfig();
            /*
            string subsCheckPath = Path.Combine(executablePath, "subs-check.exe");
            if (File.Exists(subsCheckPath)) button1.Enabled = true;
            else 
            {
                Log("没有找到 subs-check.exe 文件。", true);
                MessageBox.Show("缺少 subs-check.exe 核心文件。\n\n您可以前往 https://github.com/beck-8/subs-check/releases 自行下载！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            */
            await CheckGitHubVersionAsync();
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
                    if (speedTestUrl != null)  comboBox2.Text = speedTestUrl;

                    string savemethod = 读取config字符串(config, "save-method");
                    if (savemethod != null)
                    {
                        if (savemethod == "local") comboBox1.Text = "本地";
                        else  comboBox1.Text = savemethod;
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取配置文件时发生错误: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

                // 保存listen-port
                config["listen-port"] = $@"127.0.0.1:{numericUpDown6.Value}";
                // 保存sub-store-port
                config["sub-store-port"] = $@":{numericUpDown7.Value}";

                // 保存githubproxy
                config["githubproxy"] = comboBox3.Text;

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
                else if(comboBox3.Text == "自动选择") githubProxyURL = "";

                if (comboBox3.Text != "自动选择") githubProxyURL = $"https://{comboBox3.Text}/";

                // 保存sub-urls列表
                List<string> subUrls = new List<string>();
                string allyamlFilePath = System.IO.Path.Combine(executablePath, "output", "all.yaml");
                if (System.IO.File.Exists(allyamlFilePath))
                {
                    subUrls.Add($"http://127.0.0.1:{numericUpDown6.Value}/all.yaml");
                    Log("已加载上次测试结果。");
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
                            subUrls[i] = githubProxyURL + githubRawPrefix + subUrls[i].Substring(githubRawPrefix.Length);
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
                        downloadUrl = "https://raw.githubusercontent.com/mihomo-party-org/override-hub/main/yaml/%E5%B8%83%E4%B8%81%E7%8B%97%E7%9A%84%E8%AE%A2%E9%98%85%E8%BD%AC%E6%8D%A2.yaml";
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
                else config["mihomo-overwrite-url"] = comboBox5.Text;
                
                config["rename-node"] = checkBox1.Checked;//以节点IP查询位置重命名节点
                config["media-check"] = checkBox2.Checked;//是否开启流媒体检测
                config["keep-success-proxies"] = false;
                config["print-progress"] = true;//是否显示进度
                config["sub-urls-retry"] = 3;//重试次数(获取订阅失败后重试次数)
                config["subscheck-version"] = 当前subsCheck版本号;//当前subsCheck版本号

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
            if (button1.Text == "启动") 
            {
                run = 1;
                if (button3.Enabled==false)
                {
                    string executablePath = Path.GetDirectoryName(Application.ExecutablePath);
                    string allyamlFilePath = Path.Combine(executablePath, "output", "all.yaml");
                    button3.Enabled = File.Exists(allyamlFilePath);
                }

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
                button1.Text = "停止";
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
                button3.Enabled = false;
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
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
                button1.Text = "启动";
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
                                string executablePath = Path.GetDirectoryName(Application.ExecutablePath);
                                string zipFilePath = Path.Combine(executablePath, "subs-check_Windows_i386.zip");
                                // 如果文件已存在，先删除
                                if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

                                Log($"开始下载 {downloadUrl}");

                                // 重置进度条
                                progressBar1.Value = 0;

                                // 获取文件大小
                                HttpResponseMessage sizeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, githubProxyURL + downloadUrl));
                                long totalBytes = sizeResponse.Content.Headers.ContentLength ?? 0;

                                // 创建下载请求
                                using (var downloadResponse = await client.GetAsync(githubProxyURL + downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                                using (var contentStream = await downloadResponse.Content.ReadAsStreamAsync())
                                using (var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
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
                                }

                                Log("下载完成，正在解压文件...");

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
                                MessageBox.Show("未能找到适用的 subs-check.exe 下载链接。\n\n可尝试更换 Github Proxy 后，点击 检查更新>更新内核。\n或前往 https://github.com/beck-8/subs-check/releases 自行下载！",
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
                        MessageBox.Show($"下载 subs-check.exe 时出错: {ex.Message}\n\n可尝试更换 Github Proxy 后，点击 检查更新>更新内核。\n或前往 https://github.com/beck-8/subs-check/releases 自行下载！",
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
            }
            catch (Exception ex)
            {
                Log($"启动 subs-check.exe 时出错: {ex.Message}", true);
                button1.Text = "启动";
            }
        }


        private void StopSubsCheckProcess()
        {
            if (subsCheckProcess != null && !subsCheckProcess.HasExited)
            {
                try
                {
                    // 尝试正常关闭进程
                    subsCheckProcess.Kill();
                    subsCheckProcess.WaitForExit();
                    Log("subs-check.exe 已停止");
                    notifyIcon1.Icon = originalNotifyIcon;
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

        private string lastProgressLine = null; // 这个变量已经在类中定义，用于记录最后的进度行
        private string nextCheckTime = null;// 用于存储下次检查时间
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

                        // 确保通知图标文本不超过63个字符
                        string notifyText = "SubsCheck: " + nodeInfo + "\n" + nextCheckTime;
                        if (notifyText.Length > 63)
                        {
                            // 优先保留节点信息
                            int remainingLength = 63 - ("SubsCheck: ".Length + nodeInfo.Length);
                            if (remainingLength > 5) // 确保至少有足够空间显示部分下次检查时间
                            {
                                notifyText = "SubsCheck: " + nodeInfo + "\n" + nextCheckTime.Substring(0, Math.Min(remainingLength, nextCheckTime.Length));
                            }
                            else
                            {
                                notifyText = "SubsCheck: " + nodeInfo;
                            }
                        }
                        notifyIcon1.Text = notifyText;
                    }

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
                button1.Text = "启动";

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
                                 "· 仅在本机订阅：直接点击【取消】，将使用127.0.0.1\n\n" +
                                 "· 局域网内其他设备订阅：请在下面列表中选择一个正确的局域网IP";
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
                    listBox.SelectedIndex = 0; // 默认选择第一个IP
                    selectForm.Controls.Add(listBox);

                    // 添加警告标签（放在列表框下方）
                    Label warningLabel = new Label();
                    warningLabel.Text = "注意：选择错误的IP会导致局域网内其他设备无法正常订阅";
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

            // 滚动到最底部
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }

        private void 恢复窗口()
        {
            // 显示窗体
            this.Show();

            // 恢复窗口状态
            this.WindowState = FormWindowState.Normal;

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

        private async void timer3_Tick(object sender, EventArgs e)
        {
            if (button1.Text == "停止") 
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
                button1.Text = "停止";
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
            await ProcessComboBox5Selection(true);
        }

        private async Task ProcessComboBox5Selection(bool 汇报Log = false)
        {
            if (comboBox5.Text.Contains("[内置]"))
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
                    downloadUrl = "https://raw.githubusercontent.com/mihomo-party-org/override-hub/main/yaml/%E5%B8%83%E4%B8%81%E7%8B%97%E7%9A%84%E8%AE%A2%E9%98%85%E8%BD%AC%E6%8D%A2.yaml";
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
                                    //添加排除规则
                                    Log($"{displayName} 覆写配置文件 下载成功");
                                    AddDirectRule(Path.Combine(outputFolderPath, fileName));
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
                    //添加排除规则
                    if(!File.ReadAllText(downloadFilePath).Contains("subs-check.exe"))
                        AddDirectRule(Path.Combine(outputFolderPath, fileName));

                    if (汇报Log) Log($"{displayName} 覆写配置文件 已就绪。");
                }
            }
        }

        /// <summary>
        /// 对覆写文件添加"PROCESS-NAME,subs-check.exe,DIRECT"的前置规则，使其不受Tun模式影响
        /// </summary>
        /// <param name="filePath">文件路径</param>
        private void AddDirectRule(string filePath)
        {
            string newRule = "  - \"PROCESS-NAME,subs-check.exe,DIRECT\"";

            try
            {
                // 读取 YAML 文件内容
                string[] lines = File.ReadAllLines(filePath);

                int rulesIndex = -1;
                // 找到 rules: 所在的行
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim().StartsWith("rules:"))
                    {
                        rulesIndex = i;
                        break;
                    }
                }

                if (rulesIndex != -1)
                {
                    // 创建一个新的列表来存储修改后的行
                    List<string> newLines = new List<string>();
                    // 将 rules: 之前的行添加到新列表中
                    for (int i = 0; i < rulesIndex + 1; i++)
                    {
                        newLines.Add(lines[i]);
                    }
                    // 添加新的规则
                    newLines.Add(newRule);
                    // 将 rules: 之后的行添加到新列表中
                    for (int i = rulesIndex + 1; i < lines.Length; i++)
                    {
                        newLines.Add(lines[i]);
                    }

                    // 将修改后的内容写回文件
                    File.WriteAllLines(filePath, newLines);
                    Log("绕过subs-check.exe规则添加成功");
                }
                else
                {
                    Log("未找到 'rules:' 节点");
                }
            }
            catch (Exception ex)
            {
                Log($"添加规则错误：{ex.Message}");
            }
        }
    }
}
