using UnityEngine;

namespace DJMaster
{
    // The floating panel above the board: title, instructions and a status line, plus a row of buttons.
    public class InstructionPanel : MonoBehaviour
    {
        public TextMesh title, body, status, feedback;
        public PokeButton[] actionButtons = new PokeButton[3];
        public TextMesh[] actionLabels = new TextMesh[3];
        public int wrapChars = 52;

        string lastBody, lastTitle, lastStatus, lastFeedback;

        public void Show(string titleText, string bodyText, string statusText = "", string feedbackText = "")
        {
            if (titleText != lastTitle) { title.text = titleText; lastTitle = titleText; }
            if (bodyText != lastBody) { body.text = TextUtil.Wrap(bodyText, wrapChars); lastBody = bodyText; }
            if (statusText != lastStatus) { status.text = TextUtil.Wrap(statusText, wrapChars + 8); lastStatus = statusText; }
            if (feedbackText != lastFeedback) { feedback.text = feedbackText; lastFeedback = feedbackText; }
        }

        // Pass null to hide a button.
        public void SetActions(string a, string b, string c)
        {
            string[] labels = { a, b, c };
            for (int i = 0; i < actionButtons.Length; i++)
            {
                bool on = labels[i] != null;
                actionButtons[i].gameObject.SetActive(on);
                if (on) actionLabels[i].text = labels[i];
            }
        }
    }
}
