﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace PokemonTrackerEditor.Model {
    class DepCache {
        public enum NextType { ITEM, POKEMON, TRAINER, TRADE, LOCATION }

        public class CondCache {
            public enum CondType { AND, OR, NOT, DEP }

            public CondType Type;
            public List<CondCache> Children;
            public string Path;

            public CondCache(string path) {
                Path = path;
                Type = CondType.DEP;
            }

            public CondCache(CondType type) {
                Type = type;
                Children = new List<CondCache>();
            }

            public void ResolveDependencies(ConditionCollection collection) {
                switch (Type) {
                    case CondType.DEP:
                        collection.AddCondition(collection.ResolvePath(Path));
                        break;
                    case CondType.AND:
                    case CondType.OR:
                    case CondType.NOT:
                        ConditionCollection coll = collection.AddCollection((ConditionCollection.LogicalType)Type);
                        foreach (CondCache cache in Children) {
                            cache.ResolveDependencies(coll);
                        }
                        break;
                }
                Clear();
            }

            public void Clear() {
                if (Children != null) {
                    foreach (CondCache cache in Children) {
                        cache.Clear();
                    }
                    Children.Clear();
                    Children = null;
                }
                Path = null;
            }
        }

        public class DepCondMap {
            public DependencyEntry Entry;
            public List<CondCache> Conditions;

            public DepCondMap(DependencyEntry entry) {
                Entry = entry;
                Conditions = new List<CondCache>();
            }

            public void Clear() {
                foreach (CondCache condition in Conditions) {
                    condition.Clear();
                }
                Conditions.Clear();
                Conditions = null;
                Entry = null;
            }

            public void ResolveDependencies() {
                foreach (CondCache cache in Conditions) {
                    cache.ResolveDependencies(Entry.Conditions);
                }
            }
        }

        public static NextType nextType;

        public static RuleSet ruleSet;
        public static DependencyEntry currentDependencyEntry;
        public static List<DepCondMap> depCondMaps;
        public static List<CondCache> currentCondCacheList;
        public static ILocationContainer currentLocationContainer;
        public static StoryItemBase currentStoryItemBase;
        public static bool localizationIsStoryItem;

        public static void Init() {
            ruleSet = null;
            currentDependencyEntry = null;
            currentCondCacheList = null;
            if (depCondMaps == null) {
                depCondMaps = new List<DepCondMap>();
            }
            foreach (DepCondMap map in depCondMaps) {
                map.Clear();
            }
            depCondMaps.Clear();
            currentLocationContainer = null;
            currentStoryItemBase = null;
            localizationIsStoryItem = true;
        }
    }

    abstract class CustomConverter : JsonConverter {
        private static readonly StartObjectClass soInstance = new StartObjectClass();
        private static readonly EndObjectClass eoInstance = new EndObjectClass();
        private static readonly StartArrayClass saInstance = new StartArrayClass();
        private static readonly EndArrayClass eaInstance = new EndArrayClass();
        private static readonly StringClass sInstance = new StringClass();
        private static readonly IntClass iInstance = new IntClass();
        public static StartObjectClass StartObject => soInstance;
        public static EndObjectClass EndObject => eoInstance;
        public static StartArrayClass StartArray => saInstance;
        public static EndArrayClass EndArray => eaInstance;
        public static StringClass String => sInstance;
        public static IntClass Int => iInstance;

        public abstract class JsonParam {

            public class TokenException : System.Exception {
                public TokenException() : base() { }
                public TokenException(string message) : base(message) { }
            }

            private readonly string v;
            protected abstract JsonToken Type { get; }

            public JsonParam(string value = null) {
                v = value;
            }

            public bool Match(JsonReader reader) {
                return reader.TokenType == Type && (v == null || v.Equals(reader.Value));
            }

            public override string ToString() {
                return string.Format("({0}: {1})", Type.ToString(), v);
            }
        }

        public class Property : JsonParam {
            protected override JsonToken Type => JsonToken.PropertyName;
            private static readonly Dictionary<string, Property> instances = new Dictionary<string, Property>();
            private static readonly Property nullValue = new Property();

            private Property(string value=null) : base(value) { }

            public static Property Name(string value = null) {
                Property ret;
                if (value == null) {
                    ret = nullValue;
                }
                else if (!instances.TryGetValue(value, out ret)) {
                    ret = new Property(value);
                    instances.Add(value, ret);
                }
                return ret;
            }
        }
        public class StringClass : JsonParam { protected override JsonToken Type => JsonToken.String; public StringClass() : base() { } }
        public class IntClass : JsonParam { protected override JsonToken Type => JsonToken.Integer; public IntClass() : base() { } }
        abstract public class StaticParam : JsonParam { }
        public class StartObjectClass : StaticParam { protected override JsonToken Type => JsonToken.StartObject; public StartObjectClass() : base() { } }
        public class EndObjectClass : StaticParam { protected override JsonToken Type => JsonToken.EndObject; public EndObjectClass() : base() { } }
        public class StartArrayClass : StaticParam { protected override JsonToken Type => JsonToken.StartArray; public StartArrayClass() : base() { } }
        public class EndArrayClass : StaticParam { protected override JsonToken Type => JsonToken.EndArray; public EndArrayClass() : base() { } }


        private const bool DEBUG = true;
        private static int indentation = 0;

        public void Log(string message, bool newLine=true) {
            if (newLine) {
                Console.WriteLine(message);
                for (int i = 0; i < indentation; ++i) {
                    Console.Write(" ");
                }
            }
            else {
                Console.Write(message + " ");
            }
        }

        public void Read(JsonReader reader, params JsonParam[] validTokens) {
            reader.Read();
            if (DEBUG)
                Log("Read: " + reader.TokenType + " " + reader.Value?.ToString());
            bool valid = validTokens.Length == 0;
            foreach (JsonParam param in validTokens) {
                if (param.Match(reader)) {
                    valid = true;
                    break;
                }
            }
            if (!valid) {
                throw new JsonParam.TokenException(
                    string.Format(
                        "Token ({0}: {1}) did not match the expected list:\n{2}",
                        reader.TokenType.ToString(),
                        reader.Value,
                        string.Join<JsonParam>(", ", validTokens)
                    )
                );
            }
        }

        public string ReadPropertyString(JsonReader reader, string property) {
            Read(reader, Property.Name(property));
            return ReadAsString(reader);
        }

        public int? ReadPropertyInt(JsonReader reader, string property) {
            Read(reader, Property.Name(property));
            return ReadAsInt32(reader);
        }

        public string ReadAsString(JsonReader reader) {
            string s = reader.ReadAsString();
            if (DEBUG)
                Log("ReadAsString: " + s);
            return s;
        }

        public int? ReadAsInt32(JsonReader reader) {
            int? i = reader.ReadAsInt32();
            if (DEBUG)
                Log("ReadAsInt32: " + i);
            return i;
        }

        public string StringValue(JsonReader reader) {
            try {
                string s = reader.Value.ToString();
                if (DEBUG)
                    Log("StringValue: " + s);
                return s;
            }
            catch (Exception e) {
                Log("ERROR in StringValue: " + reader.TokenType + " " + reader.Value);
                throw e;
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (DEBUG) {
                Log("\n\n");
                Log("-----------------------------------");
                Log("Starting deserialization of type " + objectType.ToString());
            }
            return null;
        }

        public void WriteProperty(JsonWriter writer, string prop, bool newLine=true) {
            if (DEBUG) {
                Log(prop + ": ", newLine);
            }
            writer.WritePropertyName(prop);
        }

        public void WriteProperty(JsonWriter writer, string name, string value, bool newLine=true) {
            if (DEBUG) {
                Log(name + ": " + value, newLine);
            }
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public void WriteProperty(JsonWriter writer, string name, int value, bool newLine=true) {
            if (DEBUG) {
                Log(name + ": " + value, newLine);
            }
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        public void Write(JsonWriter writer, string value, bool newLine=true) {
            if (DEBUG) {
                Log(value, newLine);
            }
            writer.WriteValue(value);
        }

        public void Write(JsonWriter writer, int value, bool newLine=true) {
            if (DEBUG) {
                Log("" + value, newLine);
            }
            writer.WriteValue(value);
        }

        public void Write(JsonWriter writer, StaticParam param, bool newLine=true) {
            string v;
            if (param is StartArrayClass) {
                v = "[";
                writer.WriteStartArray();
            }
            else if (param is EndArrayClass) {
                v = "]";
                writer.WriteEndArray();
            }
            else if (param is StartObjectClass) {
                v = "{";
                writer.WriteStartObject();
            }
            else if (param is EndObjectClass) {
                v = "}";
                writer.WriteEndObject();
            }
            else {
                v = "ERROR";
            }
            if (DEBUG) {
                Log(v, newLine);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (DEBUG) {
                Log("\n\n");
                Log("-----------------------------------");
                Log("Starting serialization of type " + value.GetType().ToString());
            }
        }

        public void Indent() {
            indentation += 4;
        }

        public void Unindent() {
            indentation -= 4;
        }
    }

    class RuleSetConverter : CustomConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(RuleSet);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            base.ReadJson(reader, objectType, existingValue, serializer);
            DepCache.Init();
            RuleSet ruleSet = new RuleSet();
            DepCache.ruleSet = ruleSet;
            ruleSet.Name = ReadPropertyString(reader, "name");
            ruleSet.Game = ReadPropertyString(reader, "game");

            serializer.Deserialize(reader, typeof(PokedexRules));

            Read(reader, Property.Name("languages"));
            Indent();
            Read(reader, StartArray);
            string str = ReadAsString(reader);
            while (str != null) {
                ruleSet.SetLanguageActive(str);
                str = ReadAsString(reader);
            }
            Unindent();

            serializer.Deserialize(reader, typeof(StoryItems));

            // maps
            Read(reader, Property.Name("locations"));
            Indent();
            Read(reader, StartArray);
            DepCache.currentLocationContainer = ruleSet;
            while (serializer.Deserialize(reader, typeof(Location)) != null) ;
            Unindent();

            Read(reader, EndObject);
            foreach (DepCache.DepCondMap depCondMap in DepCache.depCondMaps) {
                depCondMap.ResolveDependencies();
                depCondMap.Clear();
            }
            DepCache.depCondMaps.Clear();

            return ruleSet;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            RuleSet ruleSet = (RuleSet)value;
            DepCache.Init();
            DepCache.ruleSet = ruleSet;

            Write(writer, StartObject);
            Indent();
            WriteProperty(writer, "name", ruleSet.Name ?? "", false);
            WriteProperty(writer, "game", ruleSet.Game ?? "", true);

            serializer.Serialize(writer, ruleSet.Pokedex);

            WriteProperty(writer, "languages");
            Formatting writerBackup = writer.Formatting; writer.Formatting = Formatting.None;
            serializer.Serialize(writer, ruleSet.ActiveLanguages);
            writer.Formatting = writerBackup;

            serializer.Serialize(writer, ruleSet.StoryItems);

            // maps

            WriteProperty(writer, "locations", false);
            Write(writer, StartArray);
            Indent();
            foreach (Location loc in ruleSet.Locations) {
                serializer.Serialize(writer, loc);
            }
            Unindent();
            Write(writer, EndArray);
            Unindent();
            Write(writer, EndObject);
        }
    }

    class StoryItemConverter : CustomConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(StoryItem);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            base.ReadJson(reader, objectType, existingValue, serializer);
            DepCache.localizationIsStoryItem = true;
            RuleSet ruleSet = DepCache.ruleSet;
            StoryItems storyItems = ruleSet.StoryItems;
            Read(reader, Property.Name("story_items"));
            Indent();
            Read(reader, StartArray);
            Read(reader, StartObject, EndArray);
            while (reader.TokenType != JsonToken.EndArray) {
                Indent();
                StoryItemCategory category = storyItems.AddStoryItemCategory(ReadPropertyString(reader, "id"));
                DepCache.currentStoryItemBase = category;
                Read(reader, Property.Name("localization"));
                category.Localization.Cleanup();
                category.Localization = (Localization)serializer.Deserialize(reader, typeof(Localization));
                Read(reader, Property.Name("items"));
                Read(reader, StartArray);
                Read(reader, StartObject, EndArray);
                while (reader.TokenType != JsonToken.EndArray) {
                    Indent();
                    StoryItem item = category.AddStoryItem(ReadPropertyString(reader, "id"));
                    DepCache.currentStoryItemBase = item;
                    item.ImageURL = ReadPropertyString(reader, "url");
                    Read(reader, Property.Name("localization"));
                    item.Localization.Cleanup();
                    item.Localization = (Localization)serializer.Deserialize(reader, typeof(Localization));
                    Read(reader, EndObject);
                    Unindent();
                    Read(reader, StartObject, EndArray);
                }
                Read(reader, EndObject);
                Unindent();
                Read(reader, StartObject, EndArray);
            }
            Unindent();
            DepCache.localizationIsStoryItem = false;
            return storyItems;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            StoryItems storyItems = (StoryItems)value;
            WriteProperty(writer, "story_items", false);
            Write(writer, StartArray);
            Indent();
            foreach (StoryItemCategory category in storyItems.Categories) {
                Write(writer, StartObject);
                WriteProperty(writer, "id", category.Id);
                WriteProperty(writer, "localization"); serializer.Serialize(writer, category.Localization);
                WriteProperty(writer, "items", false);
                Write(writer, StartArray);
                Indent();
                foreach (StoryItem item in category.Items) {
                    Write(writer, StartObject);
                    Formatting writerBackup = writer.Formatting; writer.Formatting = Formatting.None;
                    WriteProperty(writer, "id", item.Id, false);
                    WriteProperty(writer, "url", item.ImageURL, false);
                    WriteProperty(writer, "localization"); serializer.Serialize(writer, item.Localization);
                    Write(writer, EndObject);
                    writer.Formatting = writerBackup;
                }
                Unindent();
                Write(writer, EndArray);
                Write(writer, EndObject);
            }
            Unindent();
            Write(writer, EndArray);
        }
    }
    
    abstract class DependencyEntryConverter : CustomConverter {
        public override bool CanConvert(Type objectType) {
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            base.ReadJson(reader, objectType, existingValue, serializer);
            Read(reader); // StartObject or something else
            if (reader.TokenType == JsonToken.StartObject) {
                Indent();
                DependencyEntry entry;
                string id = ReadPropertyString(reader, "id");
                switch (DepCache.nextType) {
                    case DepCache.NextType.ITEM:
                        entry = (DepCache.currentLocationContainer as Location).AddCheck(id, Check.Type.ITEM);
                        break;
                    case DepCache.NextType.POKEMON:
                        entry = (DepCache.currentLocationContainer as Location).AddCheck(id, Check.Type.POKEMON);
                        break;
                    case DepCache.NextType.TRAINER:
                        entry = (DepCache.currentLocationContainer as Location).AddCheck(id, Check.Type.TRAINER);
                        break;
                    case DepCache.NextType.TRADE:
                        entry = (DepCache.currentLocationContainer as Location).AddCheck(id, Check.Type.TRADE);
                        break;
                    case DepCache.NextType.LOCATION:
                        entry = DepCache.currentLocationContainer.AddLocation(id);
                        break;
                    default:
                        Unindent();
                        return null;
                }
                DepCache.currentDependencyEntry = entry;
                Read(reader, Property.Name("localization"));
                entry.Localization.Cleanup();
                entry.Localization = (Localization)serializer.Deserialize(reader, typeof(Localization));
                Read(reader, Property.Name("conditions"), Property.Name("items"), Property.Name("pokemon"), Property.Name("trades"), Property.Name("trainers"), Property.Name("locations"), EndObject); // "conditions" or EndObject
                if (reader.TokenType == JsonToken.PropertyName && StringValue(reader) == "conditions") {
                    Read(reader, StartObject);
                    serializer.Deserialize(reader, typeof(Conditions));
                    Read(reader, Property.Name("conditions"), Property.Name("items"), Property.Name("pokemon"), Property.Name("trades"), Property.Name("trainers"), Property.Name("locations"), EndObject); // "conditions" or EndObject
                }
                Unindent();
                return entry;
            }
            else {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            DependencyEntry entry = (DependencyEntry)value;
            WriteProperty(writer, "id", entry.Id);
            WriteProperty(writer, "localization", false); serializer.Serialize(writer, entry.Localization);
            if (entry.Conditions.Count > 0) {
                serializer.Serialize(writer, entry.Conditions);
            }
        }
    }

    class LocationConverter : DependencyEntryConverter {
        private static readonly Dictionary<string, DepCache.NextType> typeMap = new Dictionary<string, DepCache.NextType>{
            { "items", DepCache.NextType.ITEM },
            { "pokemon", DepCache.NextType.POKEMON },
            { "trainers", DepCache.NextType.TRAINER },
            { "trades", DepCache.NextType.TRADE },
            { "locations", DepCache.NextType.LOCATION }
        };

        public override bool CanConvert(Type objectType) {
            return objectType == typeof(Location);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            DepCache.nextType = DepCache.NextType.LOCATION;
            if (base.ReadJson(reader, objectType, existingValue, serializer) is Location loc) {
                Indent();
                ILocationContainer parentContainer = DepCache.currentLocationContainer;
                DepCache.currentLocationContainer = loc;
                while (reader.TokenType != JsonToken.EndObject) {
                    DepCache.nextType = typeMap[StringValue(reader)];
                    Read(reader, StartArray);
                    Type t = DepCache.nextType == DepCache.NextType.LOCATION ? typeof(Location) : typeof(Check);
                    while (serializer.Deserialize(reader, t) != null) ; // EndArray afterwards
                    Read(reader, EndObject, Property.Name("items"), Property.Name("pokemon"), Property.Name("trades"), Property.Name("trainers"), Property.Name("locations"));
                }
                DepCache.currentLocationContainer = parentContainer;
                Unindent();
                return loc;
            }
            else {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            Location loc = (Location)value;
            Write(writer, StartObject);
            Indent();
            base.WriteJson(writer, value, serializer);

            Dictionary<string, List<Check>> pairs = new Dictionary<string, List<Check>> { { "items", loc.Items }, { "pokemon", loc.Pokemon }, { "trainers", loc.Trainers }, { "trades", loc.Trades } };
            foreach (KeyValuePair<string, List<Check>> pair in pairs) {
                if (pair.Value.Count > 0) {
                    WriteProperty(writer, pair.Key, false); Write(writer, StartArray);
                    Indent();
                    foreach (Check check in pair.Value) {
                        serializer.Serialize(writer, check);
                    }
                    Unindent();
                    Write(writer, EndArray);
                }
            }

            if (loc.Locations.Count > 0) {
                WriteProperty(writer, "locations", false); Write(writer, StartArray);
                Indent();
                foreach (Location nestedLoc in loc.Locations) {
                    serializer.Serialize(writer, nestedLoc);
                }
                Unindent();
                Write(writer, EndArray);
            }
            Unindent();
            Write(writer, EndObject);
        }
    }

    class CheckConverter : DependencyEntryConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(Check);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            object o = base.ReadJson(reader, objectType, existingValue, serializer);
            return o;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            Write(writer, StartObject);
            Indent();
            base.WriteJson(writer, value, serializer);
            Unindent();
            Write(writer, EndObject);
        }
    }

    class ConditionsConverter : CustomConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(ConditionCollection) || objectType == typeof(Conditions);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            base.ReadJson(reader, objectType, existingValue, serializer);
            Indent();
            string logic = ReadPropertyString(reader, "logic");
            List<DepCache.CondCache> backupCondCacheList = DepCache.currentCondCacheList;
            if (objectType == typeof(Conditions)) {
                if (!logic.Equals("AND")) {
                    throw new ArgumentException("Top level condition containers need to be of logic type 'AND'! Got " + logic);
                }
                DepCache.DepCondMap map = new DepCache.DepCondMap(DepCache.currentDependencyEntry);
                DepCache.depCondMaps.Add(map);
                DepCache.currentCondCacheList = map.Conditions;
            }
            else {
                DepCache.CondCache cache = new DepCache.CondCache((DepCache.CondCache.CondType)Enum.Parse(typeof(DepCache.CondCache.CondType), logic));
                DepCache.currentCondCacheList.Add(cache);
                DepCache.currentCondCacheList = cache.Children;
            }
            Read(reader, Property.Name("list"));
            Read(reader, StartArray);
            Read(reader, EndArray, StartObject, String);
            while (reader.TokenType != JsonToken.EndArray) {
                if (reader.TokenType == JsonToken.String) {
                    string path = StringValue(reader);
                    DepCache.currentCondCacheList.Add(new DepCache.CondCache(path));
                }
                else {
                    serializer.Deserialize(reader, typeof(ConditionCollection));
                }
                Read(reader, EndArray, StartObject, String);
            }
            Read(reader, EndObject);
            DepCache.currentCondCacheList = backupCondCacheList;
            Unindent();
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (value is Conditions) {
                WriteProperty(writer, "conditions", false);
            }
            if (value is ConditionCollection collection) {
                Write(writer, StartObject);
                Indent();
                WriteProperty(writer, "logic", collection.Type.ToString());
                WriteProperty(writer, "list", false); Write(writer, StartArray);
                foreach (ConditionBase condition in collection.Conditions) {
                    serializer.Serialize(writer, condition);
                }
                Write(writer, EndArray);
                Write(writer, EndObject);
            }
            else if (value is Condition condition) {
                Write(writer, condition.Path);
            }
            else {
                throw new ArgumentException("Not a ConditionBase: " + value.GetType());
            }
        }
    }

    class LocalizationConverter : CustomConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(Localization);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            base.ReadJson(reader, objectType, existingValue, serializer);
            Localization loc = DepCache.localizationIsStoryItem
                ? new Localization(DepCache.currentStoryItemBase)
                : new Localization(DepCache.currentDependencyEntry);
            Indent();
            Read(reader, StartObject);
            Read(reader, Property.Name(), EndObject);
            while (reader.TokenType != JsonToken.EndObject) {
                string id = StringValue(reader);
                loc[id] = ReadAsString(reader);
                Read(reader, Property.Name(), EndObject);
            }
            Unindent();
            return loc;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            Localization loc = (Localization)value;
            Write(writer, StartObject, false);
            Formatting writerBackup = writer.Formatting; writer.Formatting = Formatting.None;
            foreach (string language in DepCache.ruleSet.ActiveLanguages) {
                WriteProperty(writer, language, loc[language], false);
            }
            Write(writer, EndObject);
            writer.Formatting = writerBackup;
        }
    }

    class PokedexRulesConverter : CustomConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(PokedexRules);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            Read(reader, Property.Name("pokedex"));
            Indent();
            Read(reader, StartArray);
            int? idx = ReadAsInt32(reader);
            while (idx.HasValue) {
                DepCache.ruleSet.Pokedex.List[idx.Value - 1].Available = true;
                idx = ReadAsInt32(reader);
            }
            Unindent();
            return DepCache.ruleSet.Pokedex;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            PokedexRules rules = (PokedexRules)value;
            WriteProperty(writer, "pokedex", false);
            Formatting writerBackup = writer.Formatting; writer.Formatting = Formatting.None;
            Write(writer, StartArray, false);
            foreach (PokedexRules.Entry entry in rules.List) {
                if (entry.Available) {
                    Write(writer, entry.Nr, false);
                }
            }
            Write(writer, EndArray, false);
            writer.Formatting = writerBackup;
        }
    }

    class PokedexDataConverter : CustomConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(Pokedex);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            base.ReadJson(reader, objectType, existingValue, serializer);
            Pokedex pokedex = new Pokedex();
            Read(reader, Property.Name(), EndObject);
            while (reader.TokenType != JsonToken.EndObject) {
                string currentPosition = StringValue(reader);
                switch (currentPosition) {
                    case "overrides":
                        ReadOverrides(reader, pokedex);
                        break;
                    case "templates":
                        ReadTemplates(reader, pokedex);
                        break;
                    case "list":
                        ReadList(reader, pokedex);
                        break;
                }
                Read(reader, Property.Name(), EndObject);
            }
            return pokedex;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            throw new NotImplementedException();
        }

        private void ReadList(JsonReader reader, Pokedex pokedex) {
            Read(reader, StartArray);
            Read(reader, StartObject, EndArray);
            while (reader.TokenType != JsonToken.EndArray) {
                pokedex.AddPokemon(ReadListEntry(reader));
                Read(reader, StartObject, EndArray);
            }
        }

        private Pokedex.Entry ReadListEntry(JsonReader reader) {
            Pokedex.Entry entry = new Pokedex.Entry();
            Read(reader, Property.Name(), EndObject);
            while (reader.TokenType != JsonToken.EndObject) {
                string entryKey = StringValue(reader);
                switch (entryKey) {
                    case "localization":
                        ReadLocalization(reader, entry);
                        break;
                    case "nr":
                        entry.nr = ReadAsInt32(reader) ?? -1;
                        break;
                    case "typeA":
                        entry.typeA = ReadAsString(reader);
                        break;
                    case "typeB":
                        entry.typeB = ReadAsString(reader);
                        break;
                }
                Read(reader, Property.Name(), EndObject);
            }
            return entry;
        }

        private void ReadLocalization(JsonReader reader, Pokedex.Entry entry) {
            Read(reader, StartObject);
            Read(reader, Property.Name(), EndObject);
            while (reader.TokenType != JsonToken.EndObject) {
                entry.Localization.Add(StringValue(reader), ReadAsString(reader));
                Read(reader, Property.Name(), EndObject);
            }
        }

        private void ReadTemplates(JsonReader reader, Pokedex pokedex) {
            Read(reader, StartObject);
            Read(reader, Property.Name(), EndObject);
            while (reader.TokenType != JsonToken.EndObject) {
                string templateKey = StringValue(reader);
                List<int> templateList = new List<int>();
                Read(reader, StartArray);
                int? index = ReadAsInt32(reader);
                while (index.HasValue) {
                    templateList.Add(index.Value);
                    index = ReadAsInt32(reader);
                }
                pokedex.Templates.Add(templateKey, templateList);
                Read(reader, Property.Name(), EndObject);
            }
        }

        private void ReadOverrides(JsonReader reader, Pokedex pokedex) {
            Read(reader, StartObject);
            Read(reader, Property.Name());
            while (reader.TokenType != JsonToken.EndObject) {
                string overrideName = StringValue(reader);
                if (overrideName != null) {
                    pokedex.Overrides.Add(overrideName, ReadOverride(reader));
                }
                Read(reader, Property.Name(), EndObject);
            }
        }

        private Dictionary<string, Dictionary<string, string>> ReadOverride(JsonReader reader) {
            Read(reader, StartObject);
            Read(reader, Property.Name());
            Dictionary<string, Dictionary<string, string>> overrideData = new Dictionary<string, Dictionary<string, string>>();
            while (reader.TokenType != JsonToken.EndObject) {
                string overrideIndex = StringValue(reader);
                Dictionary<string, string> overrideIndexData = new Dictionary<string, string>();
                if (overrideIndex != null) {
                    Read(reader, StartObject);
                    Read(reader, Property.Name(), EndObject);
                    while (reader.TokenType != JsonToken.EndObject) {
                        string overrideIndexDataKey = StringValue(reader);
                        overrideIndexData.Add(overrideIndexDataKey, ReadAsString(reader));
                        Read(reader, Property.Name(), EndObject);
                    }
                }
                overrideData.Add(overrideIndex, overrideIndexData);
                Read(reader, Property.Name(), EndObject);
            }
            return null;
        }
    }
}
