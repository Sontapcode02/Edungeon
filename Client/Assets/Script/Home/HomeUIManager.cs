using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Runtime.InteropServices; // [NEW] For DllImport


// Only use this namespace in Unity Editor to open file selection window
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HomeUIManager : MonoBehaviour
{
    [Header("--- PANEL ---")]
    public GameObject joinPanel;
    public GameObject loadingPanel;
    public GameObject hostSetupPanel;

    [Header("--- JOIN ROOM UI ---")]
    public TMP_InputField nameInput;
    public TMP_InputField idRoomInput;
    public Button joinButton;
    public Button openCreatePanelButton;

    [Header("--- LOADING UI ---")]
    public TextMeshProUGUI statusText;

    [Header("--- HOST SETUP UI ---")]
    public TMP_InputField questionPathInput;
    public TMP_InputField maxPlayerInput;
    public Button confirmCreateButton;
    public Button backButton;

    [Header("--- NEW FEATURE ---")]
    public Button browseButton; // <--- Drag "Browse File" button here

    [Header("--- AUDIO ---")]
    public AudioClip homeBGM;

    // Temp variables
    private string tempHostName;
    private string tempRoomId;
    private string loadedCsvContent; // [NEW] Store content from WebGL upload

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void UploadFile(string gameObjectName, string methodName, string filter);
#endif

    void Start()
    {
        // --- AUDIO: Play Home BGM ---
        if (AudioManager.Instance != null && homeBGM != null)
        {
            AudioManager.Instance.PlayBGM(homeBGM);
        }

        ShowPanel(joinPanel);

        // --- Assign events to buttons ---
        joinButton.onClick.AddListener(OnJoinClicked);
        joinButton.onClick.AddListener(PlayClickSound); // Audio

        confirmCreateButton.onClick.RemoveListener(OnConfirmHostSetup);
        confirmCreateButton.onClick.AddListener(OnConfirmHostSetup);
        confirmCreateButton.onClick.AddListener(PlayClickSound); // Audio

        backButton.onClick.AddListener(OnBackClicked);
        backButton.onClick.AddListener(PlayClickSound); // Audio

        if (openCreatePanelButton)
        {
            openCreatePanelButton.onClick.AddListener(OnCreateRoomButton);
            openCreatePanelButton.onClick.AddListener(PlayClickSound); // Audio
        }

        // Assign event for Browse File button
        if (browseButton != null)
        {
            browseButton.onClick.AddListener(OnBrowseFileClicked);
            browseButton.onClick.AddListener(PlayClickSound); // Audio
        }

        // 1. Connect to Server immediately on game start
        SocketClient.Instance.ConnectOnly();

        // 2. Listen to events from Server
        SocketClient.Instance.OnCheckRoomResult += HandleCheckRoomResult;
        SocketClient.Instance.OnCreateRoomResult -= HandleCreateRoomResult;
        SocketClient.Instance.OnCreateRoomResult += HandleCreateRoomResult;
    }

    void PlayClickSound()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayClickSound();
    }

    void OnDestroy()
    {
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.OnCheckRoomResult -= HandleCheckRoomResult;
            SocketClient.Instance.OnCreateRoomResult -= HandleCreateRoomResult;
        }
    }

    // --- FILE BROWSE FEATURE ---
    void OnBrowseFileClicked()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Select Question File (CSV)", "", "csv");
        if (!string.IsNullOrEmpty(path))
        {
            questionPathInput.text = path;
            Debug.Log("✅ File selected: " + path);
            loadedCsvContent = null; // Reset web content if used in Editor
        }
#elif UNITY_WEBGL
        // Call JS Plugin
        // gameObjectName = "HomeUIManager" (Check GameObject name in Scene!)
        // methodName = "OnFileUploaded"
        // filter = ".csv"
        UploadFile(gameObject.name, "OnFileUploaded", ".csv");
#endif
    }

    // [NEW] Callback from WebGL
    public void OnFileUploaded(string content)
    {
        loadedCsvContent = content;
        questionPathInput.text = "File Loaded from WebGL"; // Fake path for UI
        Debug.Log("✅ CSV Content received from WebGL!");
    }

    // --- CLIENT SECTION (JOIN) ---
    void OnJoinClicked()
    {
        string playerName = nameInput.text;
        string roomId = idRoomInput.text;

        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(roomId))
        {
            ShowError("Please enter both Name and Room ID!");
            return;
        }

        // Show Loading
        ShowPanel(loadingPanel);
        statusText.text = "Checking room...";
        statusText.color = Color.yellow;

        // Send Check command
        SocketClient.Instance.SendCheckRoom(roomId);
    }

    void HandleCheckRoomResult(string result)
    {
        if (result == "FOUND")
        {
            Debug.Log("Room exists -> Entering game!");

            // Save info
            PlayerPrefs.SetString("PLAYER_NAME", nameInput.text);
            PlayerPrefs.SetString("ROOM_ID", idRoomInput.text);
            PlayerPrefs.SetInt("IS_HOST", 0); // Client
            PlayerPrefs.Save();

            SceneManager.LoadScene("Game");
        }
        else
        {
            Debug.Log("Room not found!");
            ShowError("Room not found or incorrect ID!");
            Invoke("BackToJoin", 2f);
        }
    }

    // --- HOST SECTION (CREATE) ---
    void OnCreateRoomButton()
    {
        string playerName = nameInput.text;
        if (string.IsNullOrEmpty(playerName))
        {
            ShowError("Please enter your name first!");
            return;
        }
        // Save temporary name
        tempHostName = playerName;

        // Switch to Setup panel
        ShowPanel(hostSetupPanel);
    }

    // Confirm create room button
    void OnConfirmHostSetup()
    {
        // 1. Lock button to avoid multiple clicks
        confirmCreateButton.interactable = false;

        string roomId = GenerateRoomID();
        string maxPlayers = maxPlayerInput.text;
        if (string.IsNullOrEmpty(maxPlayers)) maxPlayers = "4";

        // --- STEP 1: GET FILE PATH (FOR EDITOR/DESKTOP) --- 
        string quizFilePath = questionPathInput.text;
        if (string.IsNullOrEmpty(quizFilePath)) quizFilePath = @"D:\questions.csv"; // Default

        // --- STEP 2: READ FILE CONTENT ---
        List<QuestionData> processedQuestions = new List<QuestionData>();

        try
        {
            string[] lines;

            // Priority: WebGL Content -> Local File
            if (!string.IsNullOrEmpty(loadedCsvContent))
            {
                // Split by newline (handle various line endings)
                lines = loadedCsvContent.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            }
            else if (File.Exists(quizFilePath))
            {
                lines = File.ReadAllLines(quizFilePath);
            }
            else
            {
                ShowError("❌ File not found!");
                confirmCreateButton.interactable = true;
                return;
            }

            // Check if file is too short (only header or empty)
            if (lines.Length <= 1)
            {
                ShowError("❌ File is empty! Please check content.");
                confirmCreateButton.interactable = true;
                return; // ⛔ STOP IMMEDIATELY
            }

            // Start from i=1 to skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] parts = line.Split(','); // Split by comma

                // Check column count (Must have Question + 4 Options + Correct Answer = 6 columns)
                if (parts.Length < 6)
                {
                    // Log warning for error line (but don't stop, skip this line)
                    Debug.LogWarning($"⚠️ Line {i + 1} invalid format: {line}");
                    continue;
                }

                QuestionData q = new QuestionData();
                q.id = i;
                q.question = parts[0];

                // Fixed List here
                q.options = new List<string> { parts[1], parts[2], parts[3], parts[4] };

                if (int.TryParse(parts[5], out int correctIdx))
                {
                    q.correctIndex = correctIdx;
                }
                else
                {
                    Debug.LogWarning($"⚠️ Line {i + 1}: Correct Answer is not a number!");
                    continue;
                }

                if (parts.Length > 6) q.category = parts[6];

                processedQuestions.Add(q);
            }
        }
        catch (System.Exception ex)
        {
            ShowError("❌ Error reading file: " + ex.Message);
            confirmCreateButton.interactable = true;
            return; // ⛔ STOP IMMEDIATELY
        }

        // --- STEP 3: CHECK VALID QUESTION COUNT ---
        if (processedQuestions.Count == 0)
        {
            ShowError("❌ No valid questions found! (Check CSV format)");
            confirmCreateButton.interactable = true;
            return; // ⛔ STOP IMMEDIATELY: Don't send empty packet
        }

        // --- STEP 4: ALL GOOD -> SEND TO SERVER ---
        Debug.Log($"✅ Successfully parsed {processedQuestions.Count} questions -> Sending to Server...");

        tempRoomId = roomId;
        ShowPanel(loadingPanel);
        statusText.text = "Uploading data to Server...";
        statusText.color = Color.yellow;

        HandshakeData data = new HandshakeData
        {
            roomId = roomId,
            playerName = tempHostName,
            questionsJson = JsonConvert.SerializeObject(processedQuestions), // Pack and send
            maxPlayers = int.TryParse(maxPlayers, out int mp) ? mp : 4 // [ADDED]
        };

        SocketClient.Instance.Send(new Packet
        {
            type = "CREATE_ROOM",
            payload = JsonConvert.SerializeObject(data)
        });
    }

    // Handler when Server reports "Create Success"
    void HandleCreateRoomResult(string result)
    {
        confirmCreateButton.interactable = true;
        if (result == "SUCCESS")
        {
            // Save Prefs
            PlayerPrefs.SetString("PLAYER_NAME", tempHostName);
            PlayerPrefs.SetString("ROOM_ID", tempRoomId);
            PlayerPrefs.SetInt("IS_HOST", 1); // Host
            PlayerPrefs.SetString("MAX_PLAYERS", maxPlayerInput.text);
            PlayerPrefs.Save();

            // Now switch scene
            SceneManager.LoadScene("Game");
        }
        else
        {
            ShowError("Create room failed: " + result);
            Invoke("BackToJoin", 2f);
        }
    }

    // --- UTILS ---
    void OnBackClicked()
    {
        ShowPanel(joinPanel);
    }

    void ShowPanel(GameObject panelToShow)
    {
        joinPanel.SetActive(false);
        loadingPanel.SetActive(false);
        hostSetupPanel.SetActive(false);
        panelToShow.SetActive(true);
    }

    void ShowError(string msg)
    {
        if (loadingPanel.activeSelf)
        {
            statusText.text = msg;
            statusText.color = Color.red;
        }
        else
        {
            Debug.LogError(msg);
        }
    }

    string GenerateRoomID()
    {
        return Random.Range(100000, 999999).ToString();
    }

    void BackToJoin()
    {
        ShowPanel(joinPanel);
    }
}