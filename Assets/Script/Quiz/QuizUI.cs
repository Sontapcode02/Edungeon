using UnityEngine;
using UnityEngine.UI;

public class QuizUI : MonoBehaviour
{
    public Text questionText;
    public Button[] optionButtons;

    private int currentQuestionId;

    public void DisplayQuestion(string text, string[] options, int qId)
    {
        questionText.text = text;
        currentQuestionId = qId;

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            optionButtons[i].GetComponentInChildren<Text>().text = options[i];
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => SendAnswer(index));
            optionButtons[i].interactable = true;
        }
    }

    void SendAnswer(int index)
    {
        var answerData = new { questionId = currentQuestionId, answerIndex = index };
        string payload = Newtonsoft.Json.JsonConvert.SerializeObject(answerData);

        SocketClient.Instance.Send(new Packet
        {
            type = "ANSWER",
            payload = payload
        });
    }

    public void ShowResult(string result)
    {
        Debug.Log("Answer Result: " + result);
        if (result == "WRONG")
        {
            // Simple visual feedback
            questionText.color = Color.red;
        }
        else
        {
            questionText.color = Color.green;
        }
    }
}