using System;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CustomEditorTools
{
    public class DSYMUploader : EditorWindow
    {
        private const string WindowPath = "Tools/iOS/Upload dSYM";
        private const string ToolName = "dSYMs Upload Tool";

        private const string GooglePlistFileName = "GoogleService-Info.plist";
        private const string XcodeArchiveExtension = "xcarchive";
        private const string UploadSymbolsPath = "Pods/FirebaseCrashlytics/upload-symbols";
        private const string BashPath = "/bin/bash";

        private const string SelectProjectPathButtonName = "Select Xcode Project Path";
        private const string XCodeArchivePathButtonName = "Select Xcode Archive file";

        private const string UploadStartedMessage = "Upload started... Please wait while the dSYMs are uploaded.";
        private const string Okay = "Okay";
        private const string SelectFile = "Select file";
        private const string SelectProjectFolder = "Select project folder";
        private const string ProcessCompleted = "Process Completed!";
        private const string UploadDSYMs = "Upload dSYMs";
        private const string CopyCommand = "Copy Command";
        private const string TerminalCommand = "Terminal Command";
        private const string StopUpload = "Stop Upload";
        private const string Output = "Output";
        private const string XcodeArchiveFile = "Xcode Archive File";
        private const string XcodeProjectPath = "Xcode Project Path";
        private const string UploadStopped = "Upload stopped.";
        private const string SelectValidPathMsg = "Please select valid path";

        private static StringBuilder output;
        private static Process terminalProcess;
        private static Vector2 scrollPosition;

        private static string iOSProjectPath;
        private static string xcodeArchivePath;
        private static string command;
        private static bool isUploading = false;
        private static bool stopUpload = false;

        private static readonly GUILayoutOption UIHeight = GUILayout.Height(40f);
        private static GUIStyle style;

        [MenuItem(WindowPath)]
        public static void ShowWindow()
        {
            CleanUp();

            var window = GetWindow(typeof(DSYMUploader), true, ToolName);
            window.minSize = new Vector2(550f, 600f);
        }

        private void OnGUI()
        {
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = true };

            ShowXcodeProjectPathInput();
            ShowXcodeArchiveFilePathInput();

            EditorGUILayout.Space();

            ShowUploadButton();

            if (!string.IsNullOrEmpty(command))
            {
                ShowTerminalCommand();

                EditorGUILayout.Space();

                if (isUploading)
                {
                    ShowStopUploadButton();
                }

                ShowOutput();
                Repaint();
            }
        }

        private static void ShowOutput()
        {
            ShowBoldLabel(Output);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.Width(Screen.width), GUILayout.Height(310));

            GUILayout.Label(output?.ToString(), style);

            GUILayout.EndScrollView();
        }

        private static void ShowStopUploadButton()
        {
            CreateHorizontalLayout(() => CreateButton(StopUpload, OnStopUpload));
        }

        private static void OnStopUpload()
        {
            stopUpload = true;
            output?.AppendLine();
            output?.AppendLine(UploadStopped);
            terminalProcess?.Kill();
            terminalProcess = null;
        }

        private static void ShowTerminalCommand()
        {
            CreateHorizontalLayout(() => ShowBoldLabel(TerminalCommand));
            CreateHorizontalLayout(() =>
            {
                EditorGUILayout.LabelField(command, style);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(CopyCommand, UIHeight, GUILayout.Width(0.3f * Screen.width)))
                {
                    CopyToClipboard(command);
                }
            });
        }

        private static void ShowUploadButton()
        {
            CreateHorizontalLayout(() =>
            {
                CreateButton(UploadDSYMs, UploadDSYMsToFirebase);
            });
        }

        private static void ShowXcodeArchiveFilePathInput()
        {
            CreateHorizontalLayout(() =>
            {
                CreateVerticalLayout(() =>
                {
                    ShowBoldLabel(XcodeArchiveFile);
                    EditorGUILayout.LabelField(xcodeArchivePath, style);
                });
                GetFilePath(XCodeArchivePathButtonName, ref xcodeArchivePath, XcodeArchiveExtension);
            });
        }

        private static void ShowXcodeProjectPathInput()
        {
            CreateHorizontalLayout(() =>
            {
                CreateVerticalLayout(() =>
                {
                    ShowBoldLabel(XcodeProjectPath);
                    EditorGUILayout.LabelField(iOSProjectPath, style);
                });
                GetFolderPath(SelectProjectPathButtonName, ref iOSProjectPath);
            });
        }

        private static void UploadDSYMsToFirebase()
        {
            var inputValid = ValidateInput(iOSProjectPath, xcodeArchivePath);
            if (!inputValid)
            {
                ShowInfoPopup(ToolName, SelectValidPathMsg);
                return;
            }
            command = $"\"{iOSProjectPath}/{UploadSymbolsPath}\" -gsp \"{iOSProjectPath}/{GooglePlistFileName}\" -p ios \"{xcodeArchivePath}/dSYMs\"";

            UnityEngine.Debug.Log(command);
            stopUpload = false;
            try
            {
                RunTerminalCommand(command);
            }
            catch (InvalidOperationException e)
            {
                UnityEngine.Debug.LogError(e);
                isUploading = false;
            }
        }

        private static async void RunTerminalCommand(string command)
        {
            output = new StringBuilder();
            output.AppendLine(UploadStartedMessage);

            var args = $" -c \'{command}\'";
            await System.Threading.Tasks.Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo()
                {
                    FileName = BashPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false,
                };
                terminalProcess = new Process
                {
                    StartInfo = startInfo,
                };

                terminalProcess.OutputDataReceived += PrintOutput;
                terminalProcess.ErrorDataReceived += PrintOutput;

                terminalProcess.Start();
                isUploading = true;

                terminalProcess.BeginOutputReadLine();
                terminalProcess.BeginErrorReadLine();

                terminalProcess.WaitForExit();
                terminalProcess?.Dispose();
                terminalProcess = null;
                isUploading = false;
            });

            if (!stopUpload)
            {
                ShowInfoPopup(ToolName, ProcessCompleted);
            }
        }

        private static void PrintOutput(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrEmpty(outLine.Data))
            {
                output.AppendLine();
                output.AppendLine(outLine.Data);
            }
        }

        #region UI Elements

        private static void CreateHorizontalLayout(Action callback)
        {
            GUILayout.BeginHorizontal();
            callback?.Invoke();
            GUILayout.EndHorizontal();
        }

        private static void CreateVerticalLayout(Action callback)
        {
            GUILayout.BeginVertical();
            callback?.Invoke();
            GUILayout.EndVertical();
        }

        private static void GetFilePath(string buttonName, ref string path, string extension = "")
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(buttonName, UIHeight, GUILayout.Width(0.3f * Screen.width)))
            {
                path = EditorUtility.OpenFilePanel(SelectFile, string.Empty, extension);
            };
        }

        private static void GetFolderPath(string buttonName, ref string path)
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(buttonName, UIHeight, GUILayout.Width(0.3f * Screen.width)))
            {
                path = EditorUtility.OpenFolderPanel(SelectProjectFolder, string.Empty, string.Empty);
            }
        }

        private static void CreateButton(string buttonName, Action onClick)
        {
            if (GUILayout.Button(buttonName))
            {
                onClick?.Invoke();
            }
        }

        private static void ShowInfoPopup(string title, string message, Action callback = null)
        {
            if (EditorUtility.DisplayDialog(title, message, Okay))
            {
                callback?.Invoke();
            }
        }

        private static void ShowBoldLabel(string text)
        {
            GUILayout.Label(text, EditorStyles.boldLabel);
        }

        #endregion

        private static bool ValidateInput(string projectPath, string archivePath) =>
            !string.IsNullOrEmpty(projectPath) && !string.IsNullOrEmpty(archivePath);

        private static void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
        }

        private static void CleanUp()
        {
            if (terminalProcess != null)
            {
                terminalProcess.Kill();
                terminalProcess = null;
            }

            command = string.Empty;
            isUploading = false;
            stopUpload = false;

            if (output != null)
            {
                output.Clear();
            }

            terminalProcess = null;
            scrollPosition = Vector2.zero;
        }
    }
}