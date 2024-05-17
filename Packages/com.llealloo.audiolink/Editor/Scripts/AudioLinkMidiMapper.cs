using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace AudioLink.Editor
{
    internal class AudioLinkMidiMapper : EditorWindow
    {
        private const string _defaultConfigPath = "Packages/com.llealloo.audiolink/Runtime/Resources/AudioLinkMidiConfig.json";
        private const int _midiConfigFormatVersion = 1;
        private int selectedDeckIndex, selectedKeyIndex;

        private JToken configFormat, configVersion, midiDecks;
        private JObject midiConfig, selectedDeck, deckKeys, deckKnobs;
        private JToken[] decks;
        static AudioLinkMidiMapper()
        {
            //EditorApplication.update += DoMidiMapper;
            //EditorApplication.update -= DoMidiMapper;
        }

        /*
        private static void DoMidiMapper()
        {
            //EditorApplication.update -= DoMidiMapper;
            //OpenMidiMapper();
        }
        */

        [MenuItem("Tools/AudioLink/MIDI Mapper")]
        public static void OpenMidiMapper()
        {
            var window = GetWindow<AudioLinkMidiMapper>(false, "AudioLink MIDI Mapper");
            window.minSize = new Vector2(400, 400);
            //window.maxSize = new Vector2(600, 600);
        }

        public static void Open()
        {
            OpenMidiMapper();
        }

        static TextAsset _midiJSONConfig;

        static TextAsset midiJSONConfig
        {
            get => _midiJSONConfig;
            set => _midiJSONConfig = value;
        }

        private void SelectMIDIKey(int keyID)
        {

            selectedKeyIndex = keyID;

            DropdownField actionSelector = rootVisualElement.Q<DropdownField>("EventName");
            DropdownField keyTypeSelector = rootVisualElement.Q<DropdownField>("KeyType");
            UnsignedIntegerField keyIDSelector = rootVisualElement.Q<UnsignedIntegerField>("EventKey");

            //JObject selectedDeck = decks[selectedDeckIndex].ToObject<JObject>();

            //if (!(selectedDeck.TryGetValue("Keys", out JToken deckKeysT)
            //&& selectedDeck.TryGetValue("Knobs", out JToken deckKnobsT))) return;

            //deckKeys = deckKeysT.ToObject<JObject>();
            //deckKnobs = deckKnobsT.ToObject<JObject>();

            IList<string> keys = deckKeys.Properties().Select(p => p.Name).ToList();
            IList<string> knobs = deckKnobs.Properties().Select(p => p.Name).ToList();

            JObject cKey;

            if (keyID < keys.Count)
            {

                Debug.Log(keyID);

                cKey = deckKeys[keys[keyID]].ToObject<JObject>();
                cKey.TryGetValue("ID", out JToken action);

                //Debug.Log(keys[keyID] + (string)action + "(key)");

                actionSelector.index = actionSelector.choices.FindIndex((str) => str == keys[keyID]);
                keyTypeSelector.index = 0;
                keyIDSelector.value = (uint)action;

            } else {

                cKey = deckKnobs[knobs[keyID - keys.Count]].ToObject<JObject>();
                cKey.TryGetValue("ID", out JToken action);

                //Debug.Log(knobs[keyID - keys.Count] + (string)action + "(knob)");

                actionSelector.index = actionSelector.choices.FindIndex((str) => str == knobs[keyID - keys.Count]);
                keyTypeSelector.index = 1;
                keyIDSelector.value = (uint)action;

            }

        }

        private void RenderActionList(bool clean)
        {

            ListView deckKeyKnobs = rootVisualElement.Q<ListView>("DeckKeys");
            List<string> items = new List<string>();

            if (clean)
            {
                if (!(selectedDeck.TryGetValue("Keys", out JToken deckKeysT)
                && selectedDeck.TryGetValue("Knobs", out JToken deckKnobsT))) return;

                deckKeys = deckKeysT.ToObject<JObject>();
                deckKnobs = deckKnobsT.ToObject<JObject>();
            }

            IList<string> keys = deckKeys.Properties().Select(p => p.Name).ToList();
            IList<string> knobs = deckKnobs.Properties().Select(p => p.Name).ToList();

            for (int index = 0; index < keys.Count; index++)
            {
                JObject cKey = deckKeys[keys[index]].ToObject<JObject>();
                cKey.TryGetValue("ID", out JToken action);
                items.Add(keys[index] + ": " + (string)action + " (key)");
            }

            for (int index = 0; index < knobs.Count; index++)
            {
                JObject cKey = deckKnobs[knobs[index]].ToObject<JObject>();
                cKey.TryGetValue("ID", out JToken action);
                items.Add(knobs[index] + ": " + (string)action + " (knob)");
            }

            deckKeyKnobs.itemsSource = items;

            Func<VisualElement> makeItem = () => new Label();
            Action<VisualElement, int> bindItem = (e, i) => (e as Label).text = items[i];

            ListView listView = rootVisualElement.Q<ListView>();
            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.itemsSource = items;
            
        }

        private void ChangeKeyState()
        {

            
            DropdownField actionSelector = rootVisualElement.Q<DropdownField>("EventName");
            DropdownField keyTypeSelector = rootVisualElement.Q<DropdownField>("KeyType");
            UnsignedIntegerField keyIDSelector = rootVisualElement.Q<UnsignedIntegerField>("EventKey");

            int action = actionSelector.index;
            int keyType = keyTypeSelector.index;
            int keyID = (int)keyIDSelector.value;

            JObject jkey = new JObject();
            jkey.Add("ID", keyID);

            JToken voidKey;
            string actionName = actionSelector.choices.ElementAt(action);

            if (deckKeys.TryGetValue(actionName, out voidKey)) deckKeys.Remove(actionName);
            if (deckKnobs.TryGetValue(actionName, out voidKey)) deckKnobs.Remove(actionName);

            if (keyType == 0)
            {

                deckKeys.Add(actionName, jkey);

                selectedDeck.Remove("Keys");
                selectedDeck.Add("Keys", deckKeys);

            } else {

                deckKnobs.Add(actionName, jkey);

                selectedDeck.Remove("Knobs");
                selectedDeck.Add("Knobs", deckKnobs);
            }

            RenderActionList(false);
            
        }

        private void SelectMIDIDeck(int deckID)
        {
            
            DropdownField deckSelector = rootVisualElement.Q<DropdownField>("Decks");

            TextField deckNameField = rootVisualElement.Q<TextField>("DeckName");
            TextField deckManufacturerField = rootVisualElement.Q<TextField>("DeckManufacturer");

            selectedDeck = decks[deckID].ToObject<JObject>();

            if (!(selectedDeck.TryGetValue("Name", out JToken deckNameC)
            && selectedDeck.TryGetValue("Manufacturer", out JToken deckManufacturerC)
            && selectedDeck.TryGetValue("Keys", out JToken deckKeysC)
            && selectedDeck.TryGetValue("Knobs", out JToken deckKnobsC))) return;

            selectedDeckIndex = deckID;

            deckNameField.value = (string)deckNameC;
            deckManufacturerField.value = (string)deckManufacturerC;

            RenderActionList(true);

            deckSelector.index = deckID;

            SelectMIDIKey(0);

        }

        private void HandleMIDIConfigSelect()
        {

            DropdownField deckSelector = rootVisualElement.Q<DropdownField>("Decks");
            

            deckSelector.choices.Clear();
            

            decks = midiDecks.ToArray();
            for (int index = 0; index < decks.Length; index++) {
                JObject currentDeck = decks[index].ToObject<JObject>();

                if (!(currentDeck.TryGetValue("Name", out JToken deckNameC)
                && currentDeck.TryGetValue("Manufacturer", out JToken deckManufacturerC)
                && currentDeck.TryGetValue("Keys", out JToken deckKeysC)
                && currentDeck.TryGetValue("Knobs", out JToken deckKnobsC))) continue;

                //Debug.Log((string)deckName);
                deckSelector.choices.Add((string)deckNameC);

                //Debug.Log((string)deckManufacturerC);
                //Debug.Log(deckKeysC);
                //Debug.Log(deckKnobsC);
            }

            // Update list display, yes this is required, thanks Unity. :/
            deckSelector.index = Math.Clamp(decks.Length - 1, 0, 1);
            deckSelector.index = 0;

            selectedDeck = decks[selectedDeckIndex].ToObject<JObject>();

            RenderActionList(true);

        }

        private void HandleFileChange()
        {

            bool valid = false;
            string errMsg = "No file selected!";

            // Has a file, check config validity
            if (midiJSONConfig != null)
            {

                try {

                    // Try parse config as JSON
                    midiConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(midiJSONConfig.text);

                    // Get JSON info
                    if (midiConfig.TryGetValue("ConfigFormat", out configFormat)
                    && midiConfig.TryGetValue("ConfigVersion", out configVersion)
                    && midiConfig.TryGetValue("MidiDecks", out midiDecks))
                    
                    // Check JSON's formatting version
                    if ((int)configFormat == _midiConfigFormatVersion)
                    {

                        // Parse MIDI config
                        HandleMIDIConfigSelect();
                        valid = true;

                    } else { // Incorrect JSON format version
                        errMsg = "Wrong config version! " + _midiConfigFormatVersion + " != " + (int)configFormat;
                        Debug.LogError("[AudioLink.MIDI] Error: Wrong MIDI config version! Expected " + _midiConfigFormatVersion + " got " + (int)configFormat + "!\nPlease check for updates!");
                    }

                } catch { // JSON parse error
                    errMsg = "Invalid MIDI Config!";
                    Debug.LogError("[AudioLink.MIDI] Error: Failed to parse MIDI config JSON!");
                }

            } // Doesn't have a file, hide UI, show error

            // Show standard configurator
            rootVisualElement.Q<VisualElement>("Selector").visible = valid;
            rootVisualElement.Q<VisualElement>("Options").visible = valid;

            // Show parser error
            rootVisualElement.Q<Label>("ErrorLabel").text = errMsg;
            rootVisualElement.Q<VisualElement>("ErrorPanel").visible = !valid;

        }

        private void CreateGUI()
        {
            // Load uXML file
            VisualTreeAsset midiMapperPanel = Resources.Load<VisualTreeAsset>("MidiMapperPanel");
            midiMapperPanel.CloneTree(rootVisualElement);

            // Get file selector
            UnityEditor.UIElements.ObjectField midiConfigSelector = rootVisualElement.Q<UnityEditor.UIElements.ObjectField>("MidiConfig");

            // Refrence deck selector
            DropdownField deckSelector = rootVisualElement.Q<DropdownField>("Decks");

            // Refrence configure options
            DropdownField actionSelector = rootVisualElement.Q<DropdownField>("EventName");
            DropdownField keyTypeSelector = rootVisualElement.Q<DropdownField>("KeyType");
            UnsignedIntegerField keyIDSelector = rootVisualElement.Q<UnsignedIntegerField>("EventKey");

            UnityEngine.UIElements.Button updateKeyButton = rootVisualElement.Q<UnityEngine.UIElements.Button>("UpdateKeyButton");
            UnityEngine.UIElements.Button saveButton = rootVisualElement.Q<UnityEngine.UIElements.Button>("SaveButton");

            // Setup List
            ListView listView = rootVisualElement.Q<ListView>();
            listView.selectionType = SelectionType.Multiple;
            // Callback invoked when the user selects an item
            //listView.itemsChosen
            listView.selectionChanged += (eventAction) => {
                selectedKeyIndex = Math.Clamp(listView.selectedIndex, 0, deckKeys.Count + deckKnobs.Count);
                SelectMIDIKey(selectedKeyIndex);
            };

            listView.itemsAdded += (eventAction) => {

                int keyID = eventAction.First();
                //Debug.Log(keyID);

                //ListView deckKeyKnobs = rootVisualElement.Q<ListView>("DeckKeys");

                JObject eventObject = new JObject();
                eventObject.Add("ID", (int)keyIDSelector.value);

                //Debug.Log(jobj);

                string eventName = actionSelector.choices.ElementAt(actionSelector.index);

                if (keyTypeSelector.index == 0)
                {

                    if (deckKeys.TryAdd(eventName, eventObject))
                    {

                        selectedDeck.Remove("Keys");
                        selectedDeck.Add("Keys", deckKeys);

                    } else Debug.Log("Key event \"" + eventName + "\" already exists!");

                } else {

                    if (deckKnobs.TryAdd(eventName, eventObject))
                    {

                        selectedDeck.Remove("Knobs");
                        selectedDeck.Add("Knobs", deckKnobs);

                    } else Debug.Log("Knob event \"" + eventName + "\" already exists!");

                }

                RenderActionList(false);

                //Debug.Log(selectedDeck);

            };

            listView.itemsRemoved += (eventAction) => {

                int keyID = eventAction.First();
                //Debug.Log(keyID);

                IList<string> keys = deckKeys.Properties().Select(p => p.Name).ToList();
                IList<string> knobs = deckKnobs.Properties().Select(p => p.Name).ToList();

                if (keyID < keys.Count)
                {

                    deckKeys.Remove(keys[keyID]);

                } else {

                    deckKnobs.Remove(knobs[keyID - keys.Count]);

                }

                RenderActionList(false);

            };

            // Handle slection change
            deckSelector.RegisterValueChangedCallback((eventAction) =>
            {
                selectedDeckIndex = deckSelector.index;
                SelectMIDIDeck(selectedDeckIndex);
            });

            updateKeyButton.clicked += () =>
            {

                ChangeKeyState();

            };

            saveButton.clicked += () =>
            {

                JObject outObj = new JObject();
                outObj.Add("ConfigFormat", midiConfig.GetValue("ConfigFormat"));
                outObj.Add("ConfigVersion", midiConfig.GetValue("ConfigVersion"));
                decks.SetValue(selectedDeck, selectedDeckIndex);
                
                outObj.Add("MidiDecks", JToken.FromObject(decks));
                string outdata = Newtonsoft.Json.JsonConvert.SerializeObject(outObj);
                Debug.Log(outdata);

            };

            // Handle key changes
            //actionSelector.RegisterValueChangedCallback((eventAction) => ChangeKeyState());
            //keyTypeSelector.RegisterValueChangedCallback((eventAction) => ChangeKeyState());
            //keyIDSelector.RegisterValueChangedCallback((eventAction) => ChangeKeyState());

            // Handle on config file change
            midiConfigSelector.RegisterCallback<ChangeEvent<UnityEngine.Object>>((eventAction) =>
            {
                midiJSONConfig = eventAction.newValue as TextAsset;
                HandleFileChange();
            });

            // Set default config as selected
            midiConfigSelector.value = AssetDatabase.LoadAssetAtPath<TextAsset>(_defaultConfigPath);
        }
    }
}

/*
[System.Serializable]
public class KeyInfo {
    public int ID;
}

[System.Serializable]
public class MidiKeys {
    public KeyInfo Enabled;
    public KeyInfo ColorChord;
    public KeyInfo AutoGain;
    public KeyInfo Reset;

    public KeyInfo BandSelect0;
    public KeyInfo BandSelect1;
    public KeyInfo BandSelect2;
    public KeyInfo BandSelect3;
}

[System.Serializable]
public class MidiKnobs {
    public KeyInfo Gain;
    public KeyInfo Bass;
    public KeyInfo Treble;
    public KeyInfo Length;
    public KeyInfo Falloff;

    public KeyInfo BandSelectHue;
    public KeyInfo BandSelectSaturation;
    public KeyInfo BandSelectValue;
    public KeyInfo BandSelectThreshold;
    public KeyInfo BandSelectStart;
}

[System.Serializable]
public class MidiDeck {
    public string Name;
    public string Manufacturer;
    public MidiKeys Keys;
    public MidiKnobs Knobs;
    //public Dictionary<string, MidiKey> Keys { get; set; }
}

[System.Serializable]
public class MidiConfig {
    public int ConfigFormat;
    public string ConfigVersion;
    public List<MidiDeck> MidiDecks;
    //public List<Dictionary<string, Dictionary<string, int>>> MidiDecks;

    public static MidiConfig CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<MidiConfig>(jsonString);
    }
}
*/