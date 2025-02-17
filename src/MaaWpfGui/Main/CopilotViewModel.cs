// <copyright file="CopilotViewModel.cs" company="MaaAssistantArknights">
// MaaWpfGui - A part of the MaaCoreArknights project
// Copyright (C) 2021 MistEO and Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylet;
using StyletIoC;
using DataFormats = System.Windows.Forms.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using Screen = Stylet.Screen;

namespace MaaWpfGui
{
    /// <summary>
    /// The view model of copilot.
    /// </summary>
    public class CopilotViewModel : Screen
    {
        private readonly IWindowManager _windowManager;
        private readonly IContainer _container;

        private AsstProxy _asstProxy;
        private SettingsViewModel _settingsViewModel;

        /// <summary>
        /// Gets or sets the view models of log items.
        /// </summary>
        public ObservableCollection<LogItemViewModel> LogItemViewModels { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopilotViewModel"/> class.
        /// </summary>
        /// <param name="container">The IoC container.</param>
        /// <param name="windowManager">The window manager.</param>
        public CopilotViewModel(IContainer container, IWindowManager windowManager)
        {
            _container = container;
            _windowManager = windowManager;
            DisplayName = Localization.GetString("Copilot");
            LogItemViewModels = new ObservableCollection<LogItemViewModel>();
            AddLog(
                Localization.GetString("CopilotTip") + "\n\n" +
                /* Localization.GetString("CopilotTip1") + "\n\n" + */
                "1. " + Localization.GetString("CopilotTip2") + "\n\n" +
                "2. " + Localization.GetString("CopilotTip3") + "\n\n" +
                "3. " + Localization.GetString("CopilotTip4"),
                /* Localization.GetString("CopilotTip5"),*/
                UILogColor.Message);
        }

        protected override void OnInitialActivate()
        {
            base.OnInitialActivate();
            _asstProxy = _container.Get<AsstProxy>();
            _settingsViewModel = _container.Get<SettingsViewModel>();
        }

        /// <summary>
        /// Adds log.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="color">The font color.</param>
        /// <param name="weight">The font weight.</param>
        public void AddLog(string content, string color = UILogColor.Trace, string weight = "Regular")
        {
            LogItemViewModels.Add(new LogItemViewModel(content, color, weight));

            // LogItemViewModels.Insert(0, new LogItemViewModel(time + content, color, weight));
        }

        /// <summary>
        /// Adds log with URL.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="url">The URL.</param>
        /// <param name="color">The font color.</param>
        /// <param name="weight">The font weight.</param>
        public void AddLogWithUrl(string content, string url, string color = UILogColor.Trace, string weight = "Regular")
        {
            LogItemViewModels.Add(new LogItemViewModel(content, color, weight));

            // LogItemViewModels.Insert(0, new LogItemViewModel(time + content, color, weight));
        }

        private bool _idle = true;

        /// <summary>
        /// Gets or sets a value indicating whether it is idle.
        /// </summary>
        public bool Idle
        {
            get => _idle;
            set
            {
                _idle = value;
                NotifyOfPropertyChange(() => Idle);
            }
        }

        /// <summary>
        /// Clears log.
        /// </summary>
        public void ClearLog()
        {
            LogItemViewModels.Clear();
        }

        private string _filename = string.Empty;

        /// <summary>
        /// Gets or sets the filename.
        /// </summary>
        public string Filename
        {
            get => _filename;
            set
            {
                SetAndNotify(ref _filename, value);
                ClearLog();
                UpdateFileDoc(_filename);
            }
        }

        private const string CopilotIdPrefix = "maa://";

        private void UpdateFileDoc(string filename)
        {
            ClearLog();
            Url = CopilotUiUrl;

            string jsonStr;
            if (File.Exists(filename))
            {
                try
                {
                    jsonStr = File.ReadAllText(filename);
                }
                catch (Exception)
                {
                    AddLog(Localization.GetString("CopilotFileReadError"), UILogColor.Error);
                    return;
                }

                _isDataFromWeb = false;
            }
            else if (filename.ToLower().StartsWith(CopilotIdPrefix))
            {
                var copilotIdStr = filename.ToLower().Remove(0, CopilotIdPrefix.Length);
                int.TryParse(copilotIdStr, out int copilotID);
                jsonStr = RequestCopilotServer(copilotID);
                _isDataFromWeb = true;
            }
            else if (int.TryParse(filename, out _))
            {
                int.TryParse(filename, out int copilotID);
                jsonStr = RequestCopilotServer(copilotID);
                _isDataFromWeb = true;
            }
            else
            {
                EasterEgg(filename);
                return;
            }

            if (jsonStr != string.Empty)
            {
                ParseJsonAndShowInfo(jsonStr);
            }
        }

        private string RequestCopilotServer(int copilotID)
        {
            try
            {
                // 创建 Http 请求
                var httpWebRequest = WebRequest.Create($@"https://api.prts.plus/copilot/get/{copilotID}") as HttpWebRequest;
                httpWebRequest.Method = "GET";
                httpWebRequest.ContentType = "application/json";
                var httpWebResponse = httpWebRequest.GetResponse() as HttpWebResponse;

                // 获取输入输出流
                using var sr = new StreamReader(httpWebResponse.GetResponseStream());
                var text = sr.ReadToEnd();
                var responseObject = (JObject)JsonConvert.DeserializeObject(text);
                if (responseObject != null && responseObject.ContainsKey("status_code") && responseObject["status_code"].ToString() == "200")
                {
                    return responseObject["data"]["content"].ToString();
                }
                else
                {
                    AddLog(Localization.GetString("CopilotNoFound"), UILogColor.Error);
                    return string.Empty;
                }
            }
            catch (Exception)
            {
                AddLog(Localization.GetString("NetworkServiceError"), UILogColor.Error);
                return string.Empty;
            }
        }

        private bool _isDataFromWeb = false;
        private const string TempCopilotFile = "resource/_temp_copilot.json";
        private string TaskType = "General";

        private void ParseJsonAndShowInfo(string jsonStr)
        {
            try
            {
                var json = (JObject)JsonConvert.DeserializeObject(jsonStr);
                if (json == null)
                {
                    AddLog(Localization.GetString("CopilotJsonError"), UILogColor.Error);
                    return;
                }

                var doc = (JObject)json["doc"];

                string title = string.Empty;
                if (doc != null && doc.ContainsKey("title"))
                {
                    title = doc["title"].ToString();
                }

                if (title.Length != 0)
                {
                    string title_color = UILogColor.Message;
                    if (doc.ContainsKey("title_color"))
                    {
                        title_color = doc["title_color"].ToString();
                    }

                    AddLog(title, title_color);
                }

                string details = string.Empty;
                if (doc != null && doc.ContainsKey("details"))
                {
                    details = doc["details"].ToString();
                }

                if (details.Length != 0)
                {
                    string details_color = UILogColor.Message;
                    if (doc.ContainsKey("details_color"))
                    {
                        details_color = doc["details_color"].ToString();
                    }

                    AddLog(details, details_color);
                    {
                        Url = CopilotUiUrl;
                        var linkParser = new Regex(@"(BV.*?).{10}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        foreach (Match match in linkParser.Matches(details))
                        {
                            Url = "https://www.bilibili.com/video/" + match.Value;
                            break;
                        }

                        if (string.IsNullOrEmpty(Url))
                        {
                            linkParser = new Regex(@"(?:https?://)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                            foreach (Match m in linkParser.Matches(details))
                            {
                                Url = m.Value;
                                break;
                            }
                        }
                    }
                }

                AddLog(string.Empty, UILogColor.Message);
                int count = 0;
                foreach (var oper in json["opers"].Cast<JObject>())
                {
                    count++;
                    AddLog(string.Format("{0}, {1} 技能", oper["name"], oper["skill"]), UILogColor.Message);
                }

                if (json.ContainsKey("groups"))
                {
                    foreach (var group in json["groups"].Cast<JObject>())
                    {
                        count++;
                        string group_name = group["name"] + ": ";
                        var operInfos = new List<string>();
                        foreach (var oper in group["opers"].Cast<JObject>())
                        {
                            operInfos.Add(string.Format("{0} {1}", oper["name"], oper["skill"]));
                        }

                        AddLog(group_name + string.Join(" / ", operInfos), UILogColor.Message);
                    }
                }

                AddLog(string.Format("共 {0} 名干员", count), UILogColor.Message);

                if (json.ContainsKey("type"))
                {
                    var type = json["type"].ToString();
                    if (type == "SSS")
                    {
                        TaskType = "SSSCopilot";

                        if (json.ContainsKey("tool_men"))
                        {
                            AddLog("编队工具人：\n" + json["tool_men"].ToString(), UILogColor.Message);
                        }

                        if (json.ContainsKey("equipment"))
                        {
                            AddLog("开局装备（横向）：\n" + json["equipment"].ToString(), UILogColor.Message);
                        }

                        if (json.ContainsKey("strategy"))
                        {
                            AddLog("开局策略：" + json["strategy"].ToString(), UILogColor.Message);
                        }
                    }
                }
                else
                {
                    TaskType = "Copilot";
                }

                if (_isDataFromWeb)
                {
                    File.Delete(TempCopilotFile);
                    File.WriteAllText(TempCopilotFile, json.ToString());
                }

                AddLog(
                    "\n\n" +
                    Localization.GetString("CopilotTip") + "\n\n" +
                    Localization.GetString("CopilotTip1") + "\n\n" +
                    Localization.GetString("CopilotTip2") + "\n\n" +
                    Localization.GetString("CopilotTip3") + "\n\n" +
                    Localization.GetString("CopilotTip4"));
                /*  Localization.GetString("CopilotTip5"));*/
            }
            catch (Exception)
            {
                AddLog(Localization.GetString("CopilotJsonError"), UILogColor.Error);
            }
        }

        /// <summary>
        /// Selects file.
        /// </summary>
        public void SelectFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON|*.json",
            };

            if (dialog.ShowDialog() == true)
            {
                Filename = dialog.FileName;
            }
        }

        /// <summary>
        /// Drops file.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        public void DropFile(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var filename = ((Array)e.Data.GetData(DataFormats.FileDrop))?.GetValue(0).ToString();
            if (filename == null)
            {
                return;
            }

            if (filename.EndsWith(".json"))
            {
                Filename = filename;
            }
            else
            {
                Filename = string.Empty;
                ClearLog();
                AddLog(Localization.GetString("NotCopilotJson"), UILogColor.Error);
            }
        }

        private bool _form = false;

        /// <summary>
        /// Gets or sets a value indicating whether to use auto-formation.
        /// </summary>
        public bool Form
        {
            get => _form;
            set => SetAndNotify(ref _form, value);
        }

        public bool Loop { get; set; } = false;

        private int _loopTimes = int.Parse(ViewStatusStorage.Get("Copilot.LoopTimes", "1"));

        public int LoopTimes
        {
            get => _loopTimes;
            set
            {
                SetAndNotify(ref _loopTimes, value);
                ViewStatusStorage.Set("Copilot.LoopTimes", value.ToString());
            }
        }

        private bool _caught = false;

        /// <summary>
        /// Starts copilot.
        /// </summary>
        private async void Start()
        {
            /*
            if (_form)
            {
                AddLog(Localization.GetString("AutoSquadTip"), LogColor.Message);
            }*/
            Idle = false;

            AddLog(Localization.GetString("ConnectingToEmulator"));

            string errMsg = string.Empty;
            var task = Task.Run(() =>
            {
                return _asstProxy.AsstConnect(ref errMsg);
            });
            _caught = await task;
            if (!_caught)
            {
                AddLog(errMsg, UILogColor.Error);
                return;
            }

            if (errMsg.Length != 0)
            {
                AddLog(errMsg, UILogColor.Error);
            }

            bool ret = _asstProxy.AsstStartCopilot(_isDataFromWeb ? TempCopilotFile : Filename, Form, TaskType,
                Loop ? LoopTimes : 1);
            if (ret)
            {
                AddLog(Localization.GetString("Running"));
                if (!_settingsViewModel.AdbReplaced && !_settingsViewModel.IsAdbTouchMode())
                {
                    AddLog(Localization.GetString("AdbReplacementTips"), UILogColor.Info);
                }
            }
            else
            {
                Idle = true;
                AddLog(Localization.GetString("CopilotFileReadError"), UILogColor.Error);
            }
        }

        /// <summary>
        /// Stops copilot.
        /// </summary>
        private void Stop()
        {
            _asstProxy.AsstStop();
            Idle = true;
        }

        private const string CopilotUiUrl = "https://www.prts.plus/";
        private string _url = CopilotUiUrl;

        /// <summary>
        /// Gets or sets the copilot URL.
        /// </summary>
        public string Url
        {
            get => _url == CopilotUiUrl ? Localization.GetString("PrtsPlus") : Localization.GetString("VideoLink");
            set => SetAndNotify(ref _url, value);
        }

        /// <summary>
        /// The event handler of clicking hyperlink.
        /// </summary>
        public void Hyperlink_Click()
        {
            try
            {
                if (!string.IsNullOrEmpty(_url))
                {
                    Process.Start(new ProcessStartInfo(_url));
                }
            }
            catch (Exception)
            {
            }
        }

        private readonly string _url2 = MaaUrls.MapPrts;

        /// <summary>
        /// The event handler of clicking hyperlink.
        /// </summary>
        public void Hyperlink_Click2()
        {
            try
            {
                if (!string.IsNullOrEmpty(_url2))
                {
                    Process.Start(new ProcessStartInfo(_url2));
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 点击后移除界面中元素焦点
        /// </summary>
        /// <param name="sender">点击事件发送者</param>
        /// <param name="e">点击事件</param>
        public void MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is UIElement element))
            {
                return;
            }

            DependencyObject scope = FocusManager.GetFocusScope(element);
            FocusManager.SetFocusedElement(scope, element);
            Keyboard.ClearFocus();
        }

        /// <summary>
        /// 回车键点击后移除界面中元素焦点
        /// </summary>
        /// <param name="sender">点击事件发送者</param>
        /// <param name="e">点击事件</param>
        public void KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (!(sender is UIElement element))
            {
                return;
            }

            DependencyObject scope = FocusManager.GetFocusScope(element);
            FocusManager.SetFocusedElement(scope, element);
            Keyboard.ClearFocus();
        }

        private void EasterEgg(string text)
        {
            switch (text)
            {
                case "/help":
                    AddLog(Localization.GetString("HelloWorld"), UILogColor.Message);
                    break;
            }
        }
    }
}
