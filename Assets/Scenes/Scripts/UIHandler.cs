﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    // For toggle "interactable"
    public Button Button_Perform, Button_Undo, Button_Edit;
    public Button Button_Train, Button_Save, Button_Load, Button_Play;

    public Text text_Notification, text_GestureList;
    public GameObject Dialog_Edit,  Dialog_Load;
    public GameObject Dialog_Save, Dialog_AddGesture, Dialog_AddAudio;
    public RectTransform GestureListViewContent;
    public RectTransform FileListViewContent;

    public Image defaultimg;
    public Sprite offimage;
    public Sprite onimage;

    private GameObject gestureItemPrefab;
    private GameObject fileItemPrefab;
    private delegate void ModeChangedHandler();
    private event ModeChangedHandler ModeChanged;

    private bool isPlaying = false;
    private bool isIdentificationMode = false;
    private int selectedGestureIndex = -1;

    private void Start()
    {
        gestureItemPrefab = Resources.Load<GameObject>("Prefabs/GestureItem");
        fileItemPrefab = Resources.Load<GameObject>("Prefabs/FileItem");

        ModeChanged += OnModeChanged;
        GestureManager.Instance.OnTrainingInProgress += NotifyTrainingInProgress;
        GestureManager.Instance.OnTrainingCompleted += NotifyTrainingCompleted;

        //Button_Train.interactable = false;
        //Button_Perform.interactable = false;
        //Button_Undo.interactable = false;

        /*text_Notification.text = "Register new gesture through [Create].\n\n" +
                                    "The sample will be recorded\n" +
                                    "while [Perform] is pressed.";
        text_GestureList.text = "";*/
    }

    public void ChangeImage(bool a)
    {
        if (a)
        {
            defaultimg.sprite = onimage;
        }
        else
        {
            defaultimg.sprite = offimage;
        }
    }

    public void StartGesture()
    {
        if (!Button_Perform.interactable)
            return;
        ChangeImage(true);

        if (isIdentificationMode)
            GestureManager.Instance.StartRead();
        else
            GestureManager.Instance.StartReadAt(selectedGestureIndex);
    }

    public void StopGesture()
    {
        if (!Button_Perform.interactable)
            return;
        ChangeImage(false);

        int identifiedIndex = GestureManager.Instance.EndRead();
        if (isIdentificationMode)
        {
            var currentGesture = GestureManager.Instance.GestureList[identifiedIndex];

            if (identifiedIndex >= 0)
                text_Notification.text = $"Identified Gesture : {currentGesture.Name}\n";
            else
                text_Notification.text = "Identification fails.\nPlease retry.";

            if (isPlaying)
            {
                AudioManager.Instance.PlaySound(currentGesture);
                text_Notification.text += $"Play \"{currentGesture.Audio}\"!";
            }

        }
        else
        {
            Button_Undo.interactable = true;
            Button_Edit.interactable = true;
            Button_Train.interactable = true;

            SetTextGestureInformation();
        }
    }

    public void UndoLastGesture()
    {
        GestureManager.Instance.DeleteLastSample(selectedGestureIndex);

        SetTextGestureInformation();
    }

    public void OpenEditDialog()
    {
        Dialog_Edit.SetActive(true);
        UpdateGestureListView();
    }

    public void CloseEditDialog() => Dialog_Edit.SetActive(false);

    public void OpenLoadDialog()
    {
        Dialog_Load.SetActive(true);
        UpdateFileListView();
    }

    public void CloseLoadDialog() => Dialog_Load.SetActive(false);

    public void OpenSaveDialog() => Dialog_Save.SetActive(true);

    public void CloseSaveDialog()
    {
        var inputfield_FileName = Dialog_Save.transform.GetComponentInChildren<InputField>();

        inputfield_FileName.text = "";
        Dialog_Save.SetActive(false);
    }



    public void OpenAddGestureDialog() => Dialog_AddGesture.SetActive(true);

    public void CloseAddGestureDialog()
    {
        var inputfield_GestureName = Dialog_AddGesture.transform.GetComponentInChildren<InputField>();

        inputfield_GestureName.text = "";
        Dialog_AddGesture.SetActive(false);
    }

    public void OpenAddAudioDialog() => Dialog_AddAudio.SetActive(true);

    public void CloseAddAudioDialog()
    {
        var inputfield_AudioName = Dialog_AddAudio.transform.GetComponentInChildren<InputField>();

        inputfield_AudioName.text = "";
        Dialog_AddAudio.SetActive(false);
    }

    private void UpdateGestureListView()
    {
        var gesture = GestureManager.Instance.GestureList;
        int count = GestureManager.Instance.GestureList.Count;

        // First, remove previous children of list.
        foreach (Transform child in GestureListViewContent)
            Destroy(child.gameObject);

        // Then, load the gestures from GM.
        for (int i = 0; i < count; i++)
        {
            var gestureItem = Instantiate(gestureItemPrefab).transform;
            var name = gestureItem.Find("Text_Name").GetComponent<Text>();
            var sampleCount = gestureItem.Find("Text_SampleCount").GetComponent<Text>();
            var audio = gestureItem.Find("Text_Audio").GetComponent<Text>();
            var button_AddAudio = gestureItem.Find("Button_AddAudio").GetComponent<Button>();
            var button_Select = gestureItem.Find("Button_Select").GetComponent<Button>();
            var button_Delete = gestureItem.Find("Button_Delete").GetComponent<Button>();

            name.text = gesture[i].Name;
            sampleCount.text = $"Samples : {gesture[i].SampleCount}";
            audio.text = $"Audio : {gesture[i].Audio}";

            // 
            button_AddAudio.onClick.AddListener(() =>
            {
                selectedGestureIndex = gesture.ToList().FindIndex(g => g.Name == name.text);
                OpenAddAudioDialog();
            });

            // Select the gesture, then record and add samples.
            button_Select.onClick.AddListener(() =>
            {
                selectedGestureIndex = gesture.ToList().FindIndex(g => g.Name == name.text);
                CloseEditDialog();

                Button_Perform.interactable = true;
                Button_Undo.interactable = true;
                isIdentificationMode = false;
                ModeChanged();

                SetTextGestureInformation();
            });

            // Delete the gesture, then update the listview.
            button_Delete.onClick.AddListener(() =>
            {
                int index = gesture.ToList().FindIndex(g => g.Name == name.text);

                GestureManager.Instance.Delete(index);

                text_Notification.text = "";
                Button_Perform.interactable = false;

                UpdateGestureListView();
            });

            gestureItem.SetParent(GestureListViewContent, false);
        }
    }

    private void UpdateFileListView()
    {
        string filePath;
#if UNITY_EDITOR
        filePath = "Assets/Data";
#elif UNITY_ANDROID
        filePath = Application.persistentDataPath;
#else
        filePath = Application.streamingAssetsPath;
#endif

        foreach (Transform child in FileListViewContent)
            Destroy(child.gameObject);

        var info = new System.IO.DirectoryInfo(filePath);
        foreach (var file in info.GetFiles())
        {
            if (file.Extension != ".dat")
                continue;

            var fileItem = Instantiate(fileItemPrefab).transform;
            var name = fileItem.Find("Text_Name").GetComponent<Text>();
            var button_Select = fileItem.Find("Button_Select").GetComponent<Button>();
            var button_Delete = fileItem.Find("Button_Delete").GetComponent<Button>();

            name.text = file.Name;

            button_Select.onClick.AddListener(() =>
            {
                Debug.Log($"{filePath}/{name.text}");
                CloseLoadDialog();
                LoadGesturesFromFile($"{filePath}/{name.text}");
            });

            button_Delete.onClick.AddListener(() =>
            {
                System.IO.File.Delete($"{filePath}/{name.text}");
                text_Notification.text = "";
                Button_Perform.interactable = false;

                UpdateFileListView();
            });

            fileItem.SetParent(FileListViewContent, false);
        }
    }

    public void AddGesture()
    {
        var inputfield_GestureName = Dialog_AddGesture.transform.GetComponentInChildren<InputField>();

        // Not allow null or whitespace as the gesture name.
        if (String.IsNullOrWhiteSpace(inputfield_GestureName.text))
            return;

        GestureManager.Instance.Register(inputfield_GestureName.text);
        inputfield_GestureName.text = "";
        Button_Perform.interactable = false;
        CloseAddGestureDialog();

        UpdateGestureListView();
    }

    public void AddAudio()
    {
        var inputfield_AudioName = Dialog_AddAudio.transform.GetComponentInChildren<InputField>();

        // Not allow null or whitespace as the audio name.
        if (String.IsNullOrWhiteSpace(inputfield_AudioName.text))
            return;

        GestureManager.Instance.GestureList[selectedGestureIndex].Audio = inputfield_AudioName.text;

        Button_Perform.interactable = false;
        inputfield_AudioName.text = "";
        text_Notification.text = "";
        CloseAddAudioDialog();

        UpdateGestureListView();
    }

    public void TrainGesture()
    {
        if (GestureManager.Instance.TryTrain())
        {
        }
        else
        {
            text_Notification.text = "Training failed.";
        }
    }

    public void Play()
    {
        if (!isPlaying)
        {
            if (GestureManager.Instance.GestureList.Count == 0)
                return;

            isPlaying = true;
            isIdentificationMode = true;
            Button_Perform.interactable = true;
            Button_Load.gameObject.SetActive(false);
            Button_Save.gameObject.SetActive(false);
            Button_Train.gameObject.SetActive(false);
            Button_Undo.gameObject.SetActive(false);
            Button_Edit.gameObject.SetActive(false);
            text_Notification.text = "Play the music\nby performing the gesture";
        }
        else
        {
            isPlaying = false;
            isIdentificationMode = false;
            Button_Perform.interactable = false;
            Button_Load.gameObject.SetActive(true);
            Button_Save.gameObject.SetActive(true);
            Button_Train.gameObject.SetActive(true);
            Button_Undo.gameObject.SetActive(true);
            Button_Edit.gameObject.SetActive(true);
        }
    }

    public void NotifyTrainingInProgress(double rate)
    {
        MainThreadCaller.Instance.Enqueue(ChangeUIWhenTrainingInProgress(rate));
    }

    private IEnumerator ChangeUIWhenTrainingInProgress(double rate)
    {
        text_Notification.text = $"Training in progress... {rate * 100:00.00}%\nPlease wait.";

        Button_Perform.interactable = false;
        Button_Edit.interactable = false;
        Button_Undo.interactable = false;
        Button_Train.interactable = false;
        Button_Load.interactable = false;
        Button_Save.interactable = false;
        Button_Play.interactable = false;

        yield return null;
    }

    public void NotifyTrainingCompleted(double rate)
    {
        MainThreadCaller.Instance.Enqueue(ChangeUIWhenTrainingCompleted(rate));
    }

    private IEnumerator ChangeUIWhenTrainingCompleted(double rate)
    {
        isIdentificationMode = true;
        ModeChanged();

        text_Notification.text = "Training finished.\n\nTest : Identify the recorded gesture.";

        Button_Perform.interactable = true;
        Button_Edit.interactable = true;
        Button_Load.interactable = true;
        Button_Save.interactable = true;
        Button_Play.interactable = true;

        yield return null;
    }


    public void Reset()
    {
        GestureManager.Instance.Initialize();
        text_Notification.text = "";

        UpdateGestureListView();
    }

    public void SaveGesturesToFile()
    {
        var inputfield_FileName = Dialog_Save.transform.GetComponentInChildren<InputField>();

        // Not allow null or whitespace as the gesture name.
        if (String.IsNullOrWhiteSpace(inputfield_FileName.text))
            return;

        if (GestureManager.Instance.SaveTrainedData(inputfield_FileName.text))
        {
            text_Notification.text = "Saving trained data succeeded.";
        }
        else
        {
            text_Notification.text = "Train must be executed before saving.";
        }

        inputfield_FileName.text = "";
        CloseSaveDialog();
    }

    public void LoadGesturesFromFile(string path)
    {
        if (GestureManager.Instance.LoadCustomTrainedData(path))
        {
            Button_Perform.interactable = true;
            isIdentificationMode = true;
            ModeChanged();

            text_Notification.text = "Loading trained data succeeded.\n";
        }
        else
        {
            text_Notification.text = "Trained data to load does not exist.";
        }
    }

    public void LoadGesturesFromDefaultFile()
    {
        if (GestureManager.Instance.LoadDeafaultTrainedData())
        {
            Button_Perform.interactable = true;
            isIdentificationMode = true;
            ModeChanged();

            text_Notification.text = "Loading trained data succeeded.\n";
        }
        else
        {
            text_Notification.text = "Trained data to load does not exist.";
        }

        CloseLoadDialog();
    }

    private void OnModeChanged()
    {
        var buttonPerformText = Button_Perform.GetComponentInChildren<Text>();

        if (isIdentificationMode)
        {
            buttonPerformText.text = "Test";
        }
        else
        {
            buttonPerformText.text = "Perform";
        }
    }

    private void SetTextGestureInformation()
    {
        var currentGesture = GestureManager.Instance.GestureList[selectedGestureIndex];

        text_Notification.text = $"Target Gesture : {currentGesture.Name}\n" +
                                    $"Samples : {currentGesture.SampleCount}\n\n" +
                                    "(At least 20 are recommended.)";
    }
}
