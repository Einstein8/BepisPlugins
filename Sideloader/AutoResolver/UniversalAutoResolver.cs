﻿using BepInEx;
using ExtensibleSaveFormat;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Logging;

namespace Sideloader.AutoResolver
{
    public static class UniversalAutoResolver
    {
        public const string UARExtID = "com.bepis.sideloader.universalautoresolver";

        public static List<ResolveInfo> LoadedResolutionInfo = new List<ResolveInfo>();

        public static void ResolveStructure(Dictionary<CategoryProperty, StructValue<int>> propertyDict, object structure, IEnumerable<ResolveInfo> extInfo, string propertyPrefix = "")
        {
            void CompatibilityResolve(KeyValuePair<CategoryProperty, StructValue<int>> kv)
            {
                //check if it's a vanilla item
                if (!ResourceRedirector.ListLoader.InternalDataList[kv.Key.Category]
                    .ContainsKey(kv.Value.GetMethod(structure)))
                {
                    //the property does not have external slot information
                    //check if we have a corrosponding item for backwards compatbility

                    //For accessories, make sure we're checking the appropriate category
                    if (kv.Key.Category.ToString().Contains("ao_"))
                    {
                        ChaFileAccessory.PartsInfo AccessoryInfo = (ChaFileAccessory.PartsInfo)structure;

                        if ((int)kv.Key.Category != AccessoryInfo.type)
                        {
                            //If the current category does not match the category saved to the card do not attempt resolving
                            return;
                        }
                    }

                    var intResolve = LoadedResolutionInfo.FirstOrDefault(x => x.Property == kv.Key.ToString()
                                                                              && x.Slot == kv.Value.GetMethod(structure)
                                                                              && x.CategoryNo == kv.Key.Category);

                    if (intResolve != null)
                    {
                        //found a match
                        Logger.Log(LogLevel.Info, $"[UAR] Compatibility resolving {intResolve.Property} from slot {kv.Value.GetMethod(structure)} to slot {intResolve.LocalSlot}");

                        kv.Value.SetMethod(structure, intResolve.LocalSlot);
                    }
                    else
                    {
                        //No match was found
                    }
                }
                else
                {
                    //not resolving since we prioritize vanilla items over modded items
                    //BepInLogger.Log($"[UAR] Not resolving item due to vanilla ID range");
                    //log commented out because it causes too much spam
                }
            }

            HashSet<string> keyHashset = new HashSet<string>();

            foreach (var kv in propertyDict)
            {
                string property = $"{propertyPrefix}{kv.Key}";

                if (extInfo != null)
                {
                    var extResolve = extInfo.FirstOrDefault(x => x.Property == property);

                    if (extResolve != null)
                    {
                        //prevent resolving 11 times per accessory
                        if (keyHashset.Contains(property))
                            continue;
                        keyHashset.Add(property);

                        //the property has external slot information 
                        var intResolve = LoadedResolutionInfo.FirstOrDefault(x => x.AppendPropertyPrefix(propertyPrefix).CanResolve(extResolve));

                        if (intResolve != null)
                        {
                            //found a match to a corrosponding internal mod
                            Logger.Log(LogLevel.Info, $"[UAR] Resolving {extResolve.GUID}:{extResolve.Property} from slot {extResolve.Slot} to slot {intResolve.LocalSlot}");

                            kv.Value.SetMethod(structure, intResolve.LocalSlot);
                        }
                        else
                        {
                            //we didn't find a match, check if we have the same GUID loaded

                            if (LoadedResolutionInfo.Any(x => x.GUID == extResolve.GUID))
                            {
                                //we have the GUID loaded, so the user has an outdated mod
                                Logger.Log(LogLevel.Warning | LogLevel.Message, $"[UAR] WARNING! Outdated mod detected! [{extResolve.GUID}]");
                            }
                            else
                            {
                                //did not find a match, we don't have the mod
                                Logger.Log(LogLevel.Warning | LogLevel.Message, $"[UAR] WARNING! Missing mod detected! [{extResolve.GUID}]");
                            }

                            kv.Value.SetMethod(structure, 999999); //set to an invalid ID
                        }
                    }
                    else //if (UnityEngine.Event.current.alt)
                    {
                        CompatibilityResolve(kv);
                    }
                }
                else
                {
                    CompatibilityResolve(kv);
                }
            }
        }

        private static int CurrentSlotID = 100000000;

        public static void GenerateResolutionInfo(Manifest manifest, ChaListData data)
        {
            var category = (ChaListDefine.CategoryNo)data.categoryNo;

            var propertyKeys = StructReference.CollatedStructValues.Keys.Where(x => x.Category == category).ToList();

            //Logger.Log(LogLevel.Debug, category.ToString());
            //Logger.Log(LogLevel.Debug, StructReference.CollatedStructValues.Count.ToString());


            foreach (var kv in data.dictList)
            {
                int newSlot = Interlocked.Increment(ref CurrentSlotID);

                //Logger.Log(LogLevel.Debug, kv.Value[0] + " | " + newSlot);

                foreach (var propertyKey in propertyKeys)
                {
                    //Logger.Log(LogLevel.Debug, propertyKey.ToString());

                    LoadedResolutionInfo.Add(new ResolveInfo
                    {
                        GUID = manifest.GUID,
                        Slot = int.Parse(kv.Value[0]),
                        LocalSlot = newSlot,
                        Property = propertyKey.ToString(),
                        CategoryNo = category
                    });

                    //Logger.Log(LogLevel.Debug, $"LOADED COUNT {LoadedResolutionInfo.Count}");
                }

                kv.Value[0] = newSlot.ToString();
            }

            //Logger.Log(LogLevel.Debug, $"Internal info count: {LoadedResolutionInfo.Count}");
            //foreach (ResolveInfo info in LoadedResolutionInfo)
            //    Logger.Log(LogLevel.Debug, $"Internal info: {info.GUID} : {info.Property} : {info.Slot}");
        }
    }
}
