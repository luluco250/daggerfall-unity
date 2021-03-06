﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2017 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:
//
// Notes:
//

using UnityEngine;
using System.Text.RegularExpressions;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Player;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Utility
{
    /// <summary>
    /// Resolves in-place text replacement macros like %var, =var_, __var, etc. for various objects.
    /// More information: http://www.dfworkshop.net/static_files/questing-source-docs.html#qrcsymbols
    /// Caller will need to supply related objects (e.g. quest) as needed.
    /// NOTE: This class will expand over time.
    /// </summary>
    public class MacroHelper
    {
        #region Fields
        #endregion

        #region Structs and Enums

        /// <summary>
        /// A macro to resolve.
        /// </summary>
        struct Macro
        {
            public string token;            // String token of any macro found (e.g. "__symbol_")
            public MacroTypes type;         // Type of macro found (MacroTypes.None if not found)
            public string symbol;           // Inner symbol of macro
            public int index;               // Index of first macro character
            public int length;              // Length of macro text from index
        }

        #endregion

        #region Public Methods
        #endregion

        #region Quests

        /// <summary>
        /// Expands any macros found inside quest message tokens.
        /// </summary>
        /// <param name="parentQuest">Parent quest of message.</param>
        /// <param name="tokens">Array of message tokens to expand macros inside of.</param>
        public void ExpandQuestMessage(Quest parentQuest, ref TextFile.Token[] tokens)
        {
            // Iterate message tokens
            for (int token = 0; token < tokens.Length; token++)
            {
                // Split token text into individual words
                string[] words = GetWords(tokens[token].text);

                // Iterate words to find macros
                for (int word = 0; word < words.Length; word++)
                {
                    Macro macro = GetMacro(words[word]);
                    if (macro.type == MacroTypes.ContextMacro)
                    {
                        // TODO: Get a quest context macro like %qdt

                        // Quick support for common tokens
                        // Will rework this later
                        if (macro.token == "%pcn")
                        {
                            // Full name
                            words[word] = words[word].Replace(macro.token, GameManager.Instance.PlayerEntity.Name);
                        }
                        else if (macro.token == "%pcf")
                        {
                            // First name
                            string[] parts = GameManager.Instance.PlayerEntity.Name.Split(' ');
                            if (parts != null && parts.Length >= 1)
                                words[word] = words[word].Replace(macro.token, parts[0]);
                        }
                        else if (macro.token == "%qdt")
                        {
                            // Quest date time
                            words[word] = words[word].Replace(macro.token, parentQuest.QuestStartTime.DateString());
                        }
                        else if (macro.token == "%ra")
                        {
                            // Race
                            words[word] = words[word].Replace(macro.token, GameManager.Instance.PlayerEntity.RaceTemplate.Name);
                        }
                        else if (macro.token == "%pct")
                        {
                            // Just use "Apprentice" for all %pct guild titles for now
                            // Guilds are not implemented yet
                            words[word] = words[word].Replace(macro.token, "Apprentice");
                        }
                        else if (macro.token == "%oth")
                        {
                            // Generate an oath
                            // TODO: Need a way of passing NPC race to oath generator
                            words[word] = words[word].Replace(macro.token, GetOath());
                        }
                        else if (macro.token == "%reg")
                        {
                            // Get current region
                            words[word] = words[word].Replace(macro.token, GetRegionName());
                        }
                        else if (macro.token == "%god")
                        {
                            // Get god of last NPC
                            words[word] = words[word].Replace(macro.token, GetGod(parentQuest));
                        }
                        else if (macro.token == "%g" || macro.token == "%g1")
                        {
                            // He/She
                            words[word] = words[word].Replace(macro.token, GetPronoun1(parentQuest));
                        }
                        else if (macro.token == "%g2")
                        {
                            // Him/Her
                            words[word] = words[word].Replace(macro.token, GetPronoun2(parentQuest));
                        }
                        else if (macro.token == "%g2self")
                        {
                            // Himself/Herself
                            words[word] = words[word].Replace(macro.token, GetPronoun2self(parentQuest));
                        }
                        else if (macro.token == "%g3")
                        {
                            // His/Hers
                            words[word] = words[word].Replace(macro.token, GetPronoun3(parentQuest));
                        }
                    }
                    else
                    {
                        // Ask resource to expand macro if possible
                        QuestResource resource = parentQuest.GetResource(macro.symbol);
                        if (resource != null)
                        {
                            string result;
                            if (resource.ExpandMacro(macro.type, out result))
                            {
                                words[word] = words[word].Replace(macro.token, result);
                            }
                        }
                    }

                    // TODO: Need to store previous macro resource for pronomial context expansions
                }

                // Reassemble words and expanded macros back into final token text
                string final = string.Empty;
                for (int i = 0; i < words.Length; i++)
                {
                    final += words[i];
                    if (i != words.Length - 1)
                        final += " ";
                }

                // Store result back into token
                tokens[token].text = final;
            }
        }

        /// <summary>
        /// Oaths by race.
        /// </summary>
        enum RacialOaths
        {
            None = 0,
            Nord = 201,
            Khajiit = 202,
            Redguard = 203,
            Breton = 204,
            Argonian = 205,
            Bosmer = 206,
            Altmer = 207,
            Dunmer = 208,
        }

        // Oaths seem to be declared by NPC race
        // Daggerfall NPCs have a limited range of races (usually Breton or Redguard).
        // Have seen Nord oaths used in Daggerfall (e.g. Mages guild questor in Gothway Garden)
        // Suspect NPCs with race: -1 (e.g. #63) get a random humanoid race within reason
        // Just returning Nord oaths for now until ready to build this out properly
        // https://www.imperial-library.info/content/daggerfall-oaths-and-expletives
        string GetOath()
        {
            return DaggerfallUnity.Instance.TextProvider.GetRandomText((int)RacialOaths.Nord);
        }

        string GetRegionName()
        {
            return GameManager.Instance.PlayerGPS.CurrentRegionName;
        }

        string GetGod(Quest quest)
        {
            // Get god of current NPC or fallback
            if (quest.LastPersonReferenced != null)
                return quest.LastPersonReferenced.GodName;
            else
                return "Arkay";
        }

        // He/She
        string GetPronoun1(Quest quest)
        {
            if (quest.LastPersonReferenced == null)
                return HardStrings.pronounHe;

            switch (quest.LastPersonReferenced.Gender)
            {
                default:
                case Game.Entity.Genders.Male:
                    return HardStrings.pronounHe;
                case Game.Entity.Genders.Female:
                    return HardStrings.pronounShe;
            }
        }

        // Him/Her
        string GetPronoun2(Quest quest)
        {
            if (quest.LastPersonReferenced == null)
                return HardStrings.pronounHim;

            switch (quest.LastPersonReferenced.Gender)
            {
                default:
                case Game.Entity.Genders.Male:
                    return HardStrings.pronounHim;
                case Game.Entity.Genders.Female:
                    return HardStrings.pronounHer;
            }
        }

        // Himself/Herself
        string GetPronoun2self(Quest quest)
        {
            if (quest.LastPersonReferenced == null)
                return HardStrings.pronounHimself;

            switch (quest.LastPersonReferenced.Gender)
            {
                default:
                case Game.Entity.Genders.Male:
                    return HardStrings.pronounHimself;
                case Game.Entity.Genders.Female:
                    return HardStrings.pronounHerself;
            }
        }

        // His/Hers
        string GetPronoun3(Quest quest)
        {
            if (quest.LastPersonReferenced == null)
                return HardStrings.pronounHis;

            switch (quest.LastPersonReferenced.Gender)
            {
                default:
                case Game.Entity.Genders.Male:
                    return HardStrings.pronounHis;
                case Game.Entity.Genders.Female:
                    return HardStrings.pronounHers;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Splits text into words.
        /// </summary>
        string[] GetWords(string text)
        {
            return text.Split(' ');
        }


        /// <summary>
        /// Attempts to get macro data from a word string.
        /// Only a single macro will be matched per word.
        /// </summary>
        /// <param name="word">Source word to inspect for macro.</param>
        /// <returns>Macro data, if a macro is found. Type will be be MacroTypes.None if no macro found in text.</returns>
        Macro GetMacro(string word)
        {
            string pattern = @"(?<prefix>____)(?<NameMacro4_Symbol>[a-zA-Z0-9.]+)(?<suffix>_)|" +
                             @"(?<prefix>___)(?<NameMacro3_Symbol>[a-zA-Z0-9.]+)(?<suffix>_)|" +
                             @"(?<prefix>__)(?<NameMacro2_Symbol>[a-zA-Z0-9.]+)(?<suffix>_)|" +
                             @"(?<prefix>_)(?<NameMacro1_Symbol>[a-zA-Z0-9.]+)(?<suffix>_)|" +
                             @"(?<prefix>==)(?<FactionMacro_Symbol>[a-zA-Z0-9.]+)(?<suffix>_)|" +
                             @"(?<prefix>=#)(?<BindingMacro_Symbol>[a-zA-Z0-9.]+)(?<suffix>_)|" +
                             @"(?<prefix>=)(?<DetailsMacro_Symbol>[a-zA-Z0-9.]+)(?<suffix>_)|" +
                             @"(?<prefix>%)(?<ContextMacro_Symbol>\w+)";

            // Mactch macro type and inner symbol
            Macro macro = new Macro();
            Match match = Regex.Match(word, pattern);
            if (match.Success)
            {
                // Get possible groups from pattern to isolate match value
                Group foundGroup = null;
                Group NameMacro4_Group = match.Groups["NameMacro4_Symbol"];
                Group NameMacro3_Group = match.Groups["NameMacro3_Symbol"];
                Group NameMacro2_Group = match.Groups["NameMacro2_Symbol"];
                Group NameMacro1_Group = match.Groups["NameMacro1_Symbol"];
                Group FactionMacro_Group = match.Groups["FactionMacro_Symbol"];
                Group BindingMacro_Group = match.Groups["BindingMacro_Symbol"];
                Group DetailsMacro_Group = match.Groups["DetailsMacro_Symbol"];
                Group ContextMacro_Group = match.Groups["ContextMacro_Symbol"];

                // Check which match group (if any) was found
                if (!string.IsNullOrEmpty(NameMacro4_Group.Value))
                {
                    macro.type = MacroTypes.NameMacro4;
                    foundGroup = NameMacro4_Group;
                }
                else if (!string.IsNullOrEmpty(NameMacro3_Group.Value))
                {
                    macro.type = MacroTypes.NameMacro3;
                    foundGroup = NameMacro3_Group;
                }
                else if (!string.IsNullOrEmpty(NameMacro2_Group.Value))
                {
                    macro.type = MacroTypes.NameMacro2;
                    foundGroup = NameMacro2_Group;
                }
                else if (!string.IsNullOrEmpty(NameMacro1_Group.Value))
                {
                    macro.type = MacroTypes.NameMacro1;
                    foundGroup = NameMacro1_Group;
                }
                else if (!string.IsNullOrEmpty(FactionMacro_Group.Value))
                {
                    macro.type = MacroTypes.FactionMacro;
                    foundGroup = FactionMacro_Group;
                }
                else if (!string.IsNullOrEmpty(BindingMacro_Group.Value))
                {
                    macro.type = MacroTypes.BindingMacro;
                    foundGroup = BindingMacro_Group;
                }
                else if (!string.IsNullOrEmpty(DetailsMacro_Group.Value))
                {
                    macro.type = MacroTypes.DetailsMacro;
                    foundGroup = DetailsMacro_Group;
                }
                else if (!string.IsNullOrEmpty(ContextMacro_Group.Value))
                {
                    macro.type = MacroTypes.ContextMacro;
                    foundGroup = ContextMacro_Group;
                }

                // Set macro data if found
                if (foundGroup != null)
                {
                    Group prefix = match.Groups["prefix"];
                    Group suffix = match.Groups["suffix"];

                    macro.symbol = foundGroup.Value;
                    macro.index = prefix.Index;

                    // Length is from first character to end of suffix (if present)
                    // This will exclude any other characters (like fullstop) adjacent to macro token in word
                    if (macro.type != MacroTypes.ContextMacro)
                        macro.length = suffix.Index + suffix.Length - macro.index;
                    else
                        macro.length = prefix.Length + macro.symbol.Length;

                    // Get substring of macro token alone
                    // This is used for replace later
                    macro.token = word.Substring(macro.index, macro.length);
                }
            }

            return macro;
        }

        #endregion
    }
}