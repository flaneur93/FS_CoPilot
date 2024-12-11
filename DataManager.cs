using System.Text.Json;
using System.IO;
using System.Windows.Input;


public class DataManager
{
    public string LoadedJsonProfile { get; private set; }
    public List<string> Categories { get; private set; } = new List<string>();
    public Dictionary<string, List<string>> Subcategories { get; private set; } = new Dictionary<string, List<string>>();
    public Dictionary<string, List<Event>> Events { get; private set; } = new Dictionary<string, List<Event>>();

    public void LoadJsonProfile(string filePath)
    {
        LoadedJsonProfile = filePath;

        string jsonContent = File.ReadAllText(filePath);
        JsonDocument jsonDocument = JsonDocument.Parse(jsonContent);

        Categories.Clear();
        Subcategories.Clear();
        Events.Clear();

        if (jsonDocument.RootElement.TryGetProperty("Js_Categories", out JsonElement categoriesElement))
        {
            foreach (var category in categoriesElement.EnumerateArray())
            {
                if (category.TryGetProperty("Js_CategoryName", out JsonElement categoryName))
                {
                    string categoryNameStr = categoryName.GetString();
                    Categories.Add(categoryNameStr);

                    if (category.TryGetProperty("Js_Subcategories", out JsonElement subcategoriesElement))
                    {
                        var subcategoryList = new List<string>();
                        foreach (var subcategory in subcategoriesElement.EnumerateArray())
                        {
                            if (subcategory.TryGetProperty("Js_SubcategoryName", out JsonElement subcategoryName))
                            {
                                string subcategoryNameStr = subcategoryName.GetString();
                                subcategoryList.Add(subcategoryNameStr);

                                if (subcategory.TryGetProperty("Js_Events", out JsonElement eventsElement))
                                {
                                    var eventList = new List<Event>();
                                    foreach (var eventElement in eventsElement.EnumerateArray())
                                    {
                                        var newEvent = new Event
                                        {
                                            JsName = eventElement.GetProperty("Js_Event").GetString(),
                                            SpeechText = eventElement.GetProperty("SpeechText").GetString(),
                                            HasParam = eventElement.GetProperty("hasParam").GetBoolean(),
                                            Id = eventElement.GetProperty("Js_id").GetString(),
                                            PType = eventElement.TryGetProperty("pType", out JsonElement pTypeElement)
                                                ? pTypeElement.GetString()
                                                : null,
                                            PMap = eventElement.TryGetProperty("pMap", out JsonElement pMapElement)
                                                ? JsonSerializer.Deserialize<Dictionary<string, string>>(pMapElement.GetRawText())
                                                : null
                                        };
                                        eventList.Add(newEvent);
                                    }
                                    Events[subcategoryNameStr] = eventList;
                                }
                            }
                        }
                        Subcategories[categoryNameStr] = subcategoryList;
                    }
                }
            }
        }
    }

    public void SaveUpdatedEventToJson(Event updatedEvent)
    {
        if (string.IsNullOrEmpty(LoadedJsonProfile))
            throw new Exception("JSON profili yüklenmedi.");

        // JSON dosyasını oku
        string jsonContent = File.ReadAllText(LoadedJsonProfile);
        var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);

        if (jsonObject != null && jsonObject.ContainsKey("Js_Categories"))
        {
            var categories = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonObject["Js_Categories"].ToString());
            foreach (var category in categories)
            {
                if (category.ContainsKey("Js_Subcategories"))
                {
                    var subcategories = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(category["Js_Subcategories"].ToString());
                    foreach (var subcategory in subcategories)
                    {
                        if (subcategory.ContainsKey("Js_Events"))
                        {
                            var events = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(subcategory["Js_Events"].ToString());
                            foreach (var ev in events)
                            {
                                if (ev.ContainsKey("Js_id") && ev["Js_id"].ToString() == updatedEvent.Id)
                                {
                                    // Güncelleme işlemi
                                    ev["SpeechText"] = updatedEvent.SpeechText;
                                }
                            }

                            // Güncellenmiş event'leri geri yaz
                            subcategory["Js_Events"] = events;
                        }
                    }

                    // Güncellenmiş subcategory'leri geri yaz
                    category["Js_Subcategories"] = subcategories;
                }
            }

            // Güncellenmiş kategorileri geri yaz
            jsonObject["Js_Categories"] = categories;
        }

        // Güncellenmiş JSON'u dosyaya yaz
        string updatedJson = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(LoadedJsonProfile, updatedJson);
    }

    public List<string> GetSpeechTexts()
    {
        var speechTexts = new List<string>();
        foreach (var subcategoryEvents in Events.Values)
        {
            foreach (var eventItem in subcategoryEvents)
            {
                if (!string.IsNullOrEmpty(eventItem.SpeechText))
                {
                    speechTexts.Add(eventItem.SpeechText);
                }
            }
        }
        return speechTexts;
    }

    public Event GetEventBySpeechText(string speechText)
    {
        foreach (var subcategoryEvents in Events.Values)
        {
            foreach (var eventItem in subcategoryEvents)
            {
                if (eventItem.SpeechText == speechText)
                {
                    return eventItem; // Eşleşen Event'i döndür
                }
            }
        }
        return null; // Eğer bulunamazsa null döndür
    }

    public Dictionary<uint, string> GetEventMappings()
    {
        var eventMappings = new Dictionary<uint, string>();

        foreach (var subcategoryEvents in Events.Values)
        {
            foreach (var eventItem in subcategoryEvents)
            {
                if (!string.IsNullOrEmpty(eventItem.JsName) && !string.IsNullOrEmpty(eventItem.Id))
                {
                    if (uint.TryParse(eventItem.Id, out uint eventID))
                    {
                        eventMappings[eventID] = eventItem.JsName;
                    }
                }
            }
        }

        return eventMappings;
    }




}

public class Event
{
    public string ?JsName { get; set; }
    public string ?SpeechText { get; set; }
    public bool ?HasParam { get; set; }
    public string  ?Id { get; set; }
    public string? PType { get; set; } // Parametre türü
    public Dictionary <string, string>? PMap { get; set; } // Parametre eşleme tablosu
}
