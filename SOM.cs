// SaveOrderManager.cs

using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Collections;
using MelonLoader.Utils;
using UnityEngine.Events;
using static Il2CppInterop.Runtime.DelegateSupport;
using Il2CppScheduleOne.UI.Shop;

[assembly: MelonInfo(typeof(SaveOrderManager.SOM), "SaveOrderManager", "1.8.2", "RAKE - MrCODGod")]
[assembly: MelonGame(null, null)]

namespace SaveOrderManager
{
    public class SOM : MelonMod
    {
        private GameObject saveButtonObj;
        private GameObject loadButtonObj;
        private GameObject savePopup;
        private GameObject loadPopup;
        private GameObject warningPopup;
        private TMP_InputField saveNameInput;
        private RectTransform cartEntryContainer;
        private Transform shopRoot;
        private Transform containerTransform;
        private readonly string savesFolderPath = Path.Combine(MelonEnvironment.UserDataDirectory, "SaveOrderManager");
        private List<string> savedOrders = new List<string>();
        private bool saveButtonCreated = false;
        private Cart cart;
        private bool monitoringCart = false;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized SaveOrderManager.");
            Directory.CreateDirectory(savesFolderPath);
            LoadExistingSaves();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
                MelonCoroutines.Start(WaitForContainer());
        }

        private IEnumerator WaitForContainer()
        {
            GameObject gasStationInterface = null;
            while (gasStationInterface == null)
            {
                gasStationInterface = GameObject.Find("GasStationInterface");
                yield return null;
            }

            containerTransform = gasStationInterface.transform.Find("Container");
            if (containerTransform == null)
            {
                LoggerInstance.Error("Container not found!");
                yield break;
            }

            while (!containerTransform.gameObject.activeInHierarchy)
                yield return null;

            cart = UnityEngine.Object.FindObjectOfType<Cart>();
            if (cart != null)
            {
                cartEntryContainer = cart.CartEntryContainer;
                shopRoot = containerTransform;
                MelonCoroutines.Start(MonitorCartAndContainer());
            }
            else
            {
                LoggerInstance.Error("Cart not found!");
            }
        }

        private IEnumerator MonitorCartAndContainer()
        {
            monitoringCart = true;
            while (monitoringCart)
            {
                if (!containerTransform.gameObject.activeInHierarchy)
                {
                    CleanupUI();
                    saveButtonCreated = false;
                }
                else
                {
                    if (HasRealCartEntries() && !saveButtonCreated)
                    {
                        CreateSaveButton();
                        saveButtonCreated = true;
                    }

                    if (loadButtonObj == null && savedOrders.Count > 0)
                        CreateLoadButton();
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private bool HasRealCartEntries()
        {
            for (int i = 0; i < cartEntryContainer.childCount; i++)
            {
                if (cartEntryContainer.GetChild(i).GetComponent<CartEntry>() != null)
                    return true;
            }
            return false;
        }

        private void CreateSaveButton()
        {
            saveButtonObj = CreateUIButton("Save Order", new Vector2(-180, -20));
            saveButtonObj.GetComponent<Button>().onClick.AddListener((UnityAction)ConvertDelegate<UnityAction>(ShowSavePopup));
        }

        private void CreateLoadButton()
        {
            loadButtonObj = CreateUIButton("Load Order", new Vector2(-360, -20));
            loadButtonObj.GetComponent<Button>().onClick.AddListener((UnityAction)ConvertDelegate<UnityAction>(TryShowLoadPopup));
        }

        private GameObject CreateUIButton(string label, Vector2 anchoredPosition)
        {
            var buttonObj = new GameObject(label + "Button");
            buttonObj.transform.SetParent(shopRoot, false);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 40);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = anchoredPosition;

            var img = buttonObj.AddComponent<Image>();
            img.color = new Color(0.85f, 0.6f, 0.3f);

            var button = buttonObj.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = img.color;
            colors.highlightedColor = new Color(0.95f, 0.7f, 0.4f);
            colors.pressedColor = new Color(0.7f, 0.5f, 0.2f);
            button.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20;
            tmp.color = Color.white;

            var textRect = tmp.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            return buttonObj;
        }

        private void TryShowLoadPopup()
        {
            if (cartEntryContainer.childCount > 0)
            {
                MelonCoroutines.Start(FlashButtonRed(loadButtonObj));
                ShowWarningPopup("Empty Cart First");
                return;
            }

            ShowLoadPopup();
        }

        private IEnumerator FlashButtonRed(GameObject buttonObj)
        {
            var img = buttonObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = Color.red;
                yield return new WaitForSeconds(0.3f);
                img.color = new Color(0.85f, 0.6f, 0.3f);
            }
        }

        private void ShowWarningPopup(string message)
        {
            if (warningPopup != null)
                GameObject.Destroy(warningPopup);

            warningPopup = new GameObject("WarningPopup");
            warningPopup.transform.SetParent(shopRoot, false);

            var rect = warningPopup.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250, 60);
            rect.anchoredPosition = new Vector2(0, 200);

            var img = warningPopup.AddComponent<Image>();
            img.color = new Color(1f, 0.8f, 0.8f, 0.95f);

            var textObj = new GameObject("WarningText");
            textObj.transform.SetParent(warningPopup.transform, false);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.color = Color.red;

            var textRect = tmp.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            MelonCoroutines.Start(DestroyAfterDelay(warningPopup, 2f));
        }

        private void ShowSavePopup()
        {
            if (savePopup != null) GameObject.Destroy(savePopup);

            savePopup = CreatePopupBackground(new Color(0.2f, 0.2f, 0.2f, 0.9f));
            savePopup.name = "SavePopup";

            var inputObj = CreateInputField(savePopup.transform, new Vector2(0, 50));
            saveNameInput = inputObj.GetComponent<TMP_InputField>();

            var confirmButton = CreateLoadButtonUI("Confirm Save", new Vector2(0, -20));
            confirmButton.transform.SetParent(savePopup.transform, false);
            confirmButton.GetComponent<Button>().onClick.AddListener((UnityAction)ConvertDelegate<UnityAction>(SaveOrder));
        }

        private void SaveOrder()
        {
            if (string.IsNullOrWhiteSpace(saveNameInput.text)) return;

            var entries = new List<SavedCartItem>();
            for (int i = 0; i < cartEntryContainer.childCount; i++)
            {
                var entry = cartEntryContainer.GetChild(i).GetComponent<CartEntry>();
                if (entry != null)
                {
                    var cleanName = entry.NameLabel.text.Contains("x") ? entry.NameLabel.text.Split('x')[1].Trim() : entry.NameLabel.text.Trim();
                    entries.Add(new SavedCartItem
                    {
                        Name = cleanName.ToLower().Replace(" ", ""),
                        Quantity = entry.Quantity
                    });
                }
            }

            var saveData = new SavedCart { Items = entries };
            File.WriteAllText(Path.Combine(savesFolderPath, saveNameInput.text + ".json"), JsonSerializer.Serialize(saveData));

            CloseSavePopup();
            LoadExistingSaves();
        }

        private void ShowLoadPopup()
        {
            if (loadPopup != null) GameObject.Destroy(loadPopup);

            loadPopup = CreatePopupBackground(new Color(1, 1, 1, 0.98f));
            loadPopup.name = "LoadPopup";

            float startY = -10;

            foreach (var saveName in savedOrders)
            {
                var buttonObj = CreateLoadButtonUI(saveName, new Vector2(0, startY));
                buttonObj.transform.SetParent(loadPopup.transform, false);
                buttonObj.GetComponent<Button>().onClick.AddListener((UnityAction)ConvertDelegate<UnityAction>(() => LoadSelectedSave(saveName)));
                startY -= 45;
            }

            var cancelButton = CreateLoadButtonUI("Cancel", new Vector2(0, startY - 20));
            cancelButton.transform.SetParent(loadPopup.transform, false);
            cancelButton.GetComponent<Button>().onClick.AddListener((UnityAction)ConvertDelegate<UnityAction>(CloseLoadPopup));
        }

        private void LoadSelectedSave(string saveName)
        {
            string path = Path.Combine(savesFolderPath, saveName + ".json");
            if (!File.Exists(path)) return;

            var saveData = JsonSerializer.Deserialize<SavedCart>(File.ReadAllText(path));
            if (saveData == null) return;

            for (int i = cartEntryContainer.childCount - 1; i >= 0; i--)
                GameObject.DestroyImmediate(cartEntryContainer.GetChild(i).gameObject);

            var content = GameObject.Find("UI/GasStationInterface/Container/Scroll View/Viewport/Content");
            if (content == null)
            {
                LoggerInstance.Error("Shop Content not found!");
                return;
            }

            foreach (var savedItem in saveData.Items)
            {
                for (int i = 0; i < content.transform.childCount; i++)
                {
                    var child = content.transform.GetChild(i);
                    var listingUI = child.GetComponent<ListingUI>();
                    if (listingUI != null)
                    {
                        var currentName = listingUI.NameLabel.text.ToLower().Replace(" ", "").Trim();
                        if (currentName == savedItem.Name)
                        {
                            for (int j = 0; j < savedItem.Quantity; j++)
                                listingUI.Clicked();
                            break;
                        }
                    }
                }
            }

            CloseLoadPopup();
        }

        private void LoadExistingSaves()
        {
            savedOrders.Clear();
            if (Directory.Exists(savesFolderPath))
            {
                foreach (var file in Directory.GetFiles(savesFolderPath, "*.json"))
                    savedOrders.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        private GameObject CreatePopupBackground(Color color)
        {
            var popup = new GameObject("Popup");
            popup.transform.SetParent(shopRoot, false);
            var rect = popup.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(350, 400);
            rect.anchoredPosition = new Vector2(0, 100);
            popup.AddComponent<Image>().color = color;
            return popup;
        }

        private GameObject CreateInputField(Transform parent, Vector2 anchoredPosition)
        {
            var inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(parent, false);

            var rect = inputObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 40);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;

            inputObj.AddComponent<Image>().color = Color.white;

            var inputField = inputObj.AddComponent<TMP_InputField>();

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.color = Color.black;
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.Center;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            inputField.textComponent = text;

            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            var placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholder.text = "Enter Save Name...";
            placeholder.color = Color.gray;
            placeholder.fontSize = 18;
            placeholder.alignment = TextAlignmentOptions.Center;

            var placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = placeholderRect.offsetMax = Vector2.zero;

            inputField.placeholder = placeholder;

            return inputObj;
        }

        private GameObject CreateLoadButtonUI(string label, Vector2 anchoredPosition)
        {
            var buttonObj = new GameObject(label + "Button");
            var rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 40);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;

            buttonObj.AddComponent<Image>().color = Color.white;
            buttonObj.AddComponent<Button>();

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20;
            tmp.color = Color.black;

            return buttonObj;
        }

        private void CloseSavePopup()
        {
            if (savePopup != null) GameObject.Destroy(savePopup);
        }

        private void CloseLoadPopup()
        {
            if (loadPopup != null) GameObject.Destroy(loadPopup);
        }

        private void CleanupUI()
        {
            if (saveButtonObj != null) GameObject.Destroy(saveButtonObj);
            if (loadButtonObj != null) GameObject.Destroy(loadButtonObj);
            if (savePopup != null) GameObject.Destroy(savePopup);
            if (loadPopup != null) GameObject.Destroy(loadPopup);
            if (warningPopup != null) GameObject.Destroy(warningPopup);
        }

        private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
                GameObject.Destroy(obj);
        }

        private class SavedCart
        {
            public List<SavedCartItem> Items { get; set; }
        }

        private class SavedCartItem
        {
            public string Name { get; set; }
            public int Quantity { get; set; }
        }
    }
}
