﻿/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System.Collections.Generic;
using System;

namespace JSI
{
    public class ResourceDataStorage
    {
        private readonly ResourceData[] rs;
        private readonly Dictionary<string, ResourceData> nameResources = new Dictionary<string, ResourceData>();
        private readonly Dictionary<string, ResourceData> sysrResources = new Dictionary<string, ResourceData>();
        private readonly string[] sortedResourceNames;
        private HashSet<Part> activeStageParts = new HashSet<Part>();
        private PartSet partSet = null;
        private int numValidResourceNames = 0;

        private class ResourceComparer : IComparer<ResourceData>
        {
            public int Compare(ResourceData a, ResourceData b)
            {
                return string.Compare(a.name, b.name);
            }
        }

        private static bool IsFreeFlow(ResourceFlowMode flowMode)
        {
            return (flowMode == ResourceFlowMode.ALL_VESSEL || flowMode == ResourceFlowMode.STAGE_PRIORITY_FLOW);
        }

        public ResourceDataStorage()
        {
            int resourceCount = PartResourceLibrary.Instance.resourceDefinitions.Count;

            rs = new ResourceData[resourceCount];
            sortedResourceNames = new string[resourceCount];
            int index = 0;
            foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions)
            {
                string nameSysr = thatResource.name.ToUpperInvariant().Replace(' ', '-').Replace('_', '-');

                rs[index] = new ResourceData();
                rs[index].name = thatResource.name;
                rs[index].density = thatResource.density;
                rs[index].resourceId = thatResource.id;
                rs[index].flowMode = thatResource.resourceFlowMode;

                nameResources.Add(thatResource.name, rs[index]);
                sysrResources.Add(nameSysr, rs[index]);
                ++index;
            }

            // Alphabetize our list
            Array.Sort(rs, new ResourceComparer());
        }

        private bool stagePartsChanged = true;
        public void ClearActiveStageParts()
        {
            activeStageParts.Clear();
            stagePartsChanged = true;
        }

        public void StartLoop(Vessel vessel)
        {
            for (int i = 0; i < rs.Length; ++i)
            {
                rs[i].stage = 0.0f;
                rs[i].stagemax = 0.0f;
                rs[i].ispropellant = false;

                double amount, maxAmount;
                vessel.GetConnectedResourceTotals(rs[i].resourceId, out amount, out maxAmount);

                rs[i].current = (float)amount;
                rs[i].max = (float)maxAmount;
                if (IsFreeFlow(rs[i].flowMode))
                {
                    rs[i].stage = (float)amount;
                    rs[i].stagemax = (float)maxAmount;
                }
            }
        }

        public void EndLoop(double dt)
        {
            float invDeltaT = (float)(1.0 / dt);
            for (int i = 0; i < rs.Length; ++i)
            {
                rs[i].delta = (rs[i].previous - rs[i].current) * invDeltaT;
                rs[i].previous = rs[i].current;
            }

            if (stagePartsChanged)
            {
                if (partSet == null)
                {
                    partSet = new PartSet(activeStageParts);
                }
                else
                {
                    partSet.RebuildParts(activeStageParts);
                }
                stagePartsChanged = false;
            }

            numValidResourceNames = 0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].max > 0.0)
                {
                    sortedResourceNames[numValidResourceNames] = rs[i].name;
                    ++numValidResourceNames;

                    // If the resource can flow anywhere, we already have the stage
                    // values listed here.
                    if (rs[i].stagemax == 0.0)
                    {
                        double amount, maxAmount;
                        partSet.GetConnectedResourceTotals(rs[i].resourceId, out amount, out maxAmount, true);
                        rs[i].stagemax = (float)maxAmount;
                        rs[i].stage = (float)amount;
                    }
                }
            }
        }

        public string GetActiveResourceByIndex(int index)
        {
            return (index < numValidResourceNames) ? sortedResourceNames[index] : string.Empty;
        }
        //public void DumpData()
        //{
        //    JUtil.LogMessage(this, "Resource data update:");
        //    for (int i = 0; i < rs.Length; ++i)
        //    {
        //        JUtil.LogMessage(this, "{0}: C {1:0.0} / {2:0.0}; T {3:0.0} / {4:0.0}; R {5:0.00}",
        //            rs[i].name, rs[i].stage, rs[i].current, rs[i].stagemax, rs[i].max, rs[i].delta);
        //    }
        //}

        public void MarkActiveStage(PartSet ps)
        {
            var parts = ps.GetParts();
            activeStageParts.UnionWith(parts);
            stagePartsChanged = true;
        }

        public void MarkPropellant(Propellant propel)
        {
            ResourceData r = nameResources[propel.name];
            r.ispropellant = true;
        }

        public void GetAvailableResourceNames(ref string[] result)
        {
            int requiredLength = 0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].max > 0.0)
                {
                    requiredLength++;
                }
            }

            if (result == null || result.Length != requiredLength)
            {
                Array.Resize(ref result, requiredLength);
            }

            int currentIndex = 0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].max > 0.0)
                {
                    result[currentIndex] = rs[i].name;
                    ++currentIndex;
                }
            }
        }

        public double PropellantMass(bool stage)
        {
            double mass = 0.0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].ispropellant)
                {
                    mass += rs[i].density * ((stage) ? rs[i].stage : rs[i].current);
                }
            }
            return mass;
        }

        private static readonly string[] keywords =
        {
            "VAL",
            "DENSITY",
            "DELTA",
            "DELTAINV",
            "MAXMASS",
            "MASS",
            "MAX",
            "PERCENT",
            "DEPLETED"
        };

        internal enum ResourceProperty
        {
            VAL,
            DENSITY,
            DELTA,
            DELTAINV,
            MAXMASS,
            MASS,
            PERCENT,
            DEPLETED
        }

        /// <summary>
        /// Given a string in the form XXXYYYZZZ where YYY may be "STAGE" or null, and ZZZ is one of the keywords above, sets the valueType to the keyword, sets the stage param to whether "STAGE" was present, returns XXX
        /// </summary>
        /// <param name="resourceQuery"></param>
        /// <param name="valueType"></param>
        /// <param name="stage"></param>
        public static string ParseResourceQuery(string resourceQuery, out string valueType, out bool stage)
        {
            valueType = "VAL";
            stage = false;

            foreach (var keyword in keywords)
            {
                if (resourceQuery.EndsWith(keyword))
                {
                    valueType = keyword;
                    resourceQuery = resourceQuery.Substring(0, resourceQuery.Length - keyword.Length);
                    break;
                }
            }

            if (resourceQuery.EndsWith("STAGE"))
            {
                stage = true;
                resourceQuery = resourceQuery.Substring(0, resourceQuery.Length - "STAGE".Length);
            }

            return resourceQuery;
        }

        public double ListSYSElement(string resourceName, string valueType, bool stage)
        {
            double v = 0.0;
            try
            {
                ResourceData resource = sysrResources[resourceName];
                switch (valueType)
                {
                    case "":
                    case "VAL":
                        v = stage ? resource.stage : resource.current;
                        break;
                    case "DENSITY":
                        v = resource.density;
                        break;
                    case "DELTA":
                        v = resource.delta;
                        break;
                    case "DELTAINV":
                        v = -resource.delta;
                        break;
                    case "MASS":
                        v = resource.density * (stage ? resource.stage : resource.current);
                        break;
                    case "MAXMASS":
                        v = resource.density * (stage ? resource.stagemax : resource.max);
                        break;
                    case "MAX":
                        v = stage ? resource.stagemax : resource.max;
                        break;
                    case "PERCENT":
                        if (stage)
                        {
                            v = resource.stagemax > 0 ? resource.stage / resource.stagemax : 0d;
                        }
                        else
                        {
                            v = resource.max > 0 ? resource.current / resource.max : 0d;
                        }
                        break;
                    case "DEPLETED":
                        if (stage)
                        {
                            bool available = (resource.stagemax > 0.0f && resource.stage < 0.01f);
                            v = available.GetHashCode();
                        }
                        else
                        {
                            bool available = (resource.max > 0.0f && resource.current < 0.01f);
                            v = available.GetHashCode();
                        }
                        break;
                }

            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "ListElement({1}) threw trapped exception {0}", e, resourceName);
                return double.NaN;
            }
            return v;
        }

        public double ListElement(string resourceName, string valueType, bool stage)
        {
            double v = 0.0;

            try
            {
                ResourceData resource = nameResources[resourceName];

                switch (valueType)
                {
                    case "":
                    case "VAL":
                        v = stage ? resource.stage : resource.current;
                        break;
                    case "DENSITY":
                        v = resource.density;
                        break;
                    case "DELTA":
                        v = resource.delta;
                        break;
                    case "DELTAINV":
                        v = -resource.delta;
                        break;
                    case "MASS":
                        v = resource.density * (stage ? resource.stage : resource.current);
                        break;
                    case "MAXMASS":
                        v = resource.density * (stage ? resource.stagemax : resource.max);
                        break;
                    case "MAX":
                        v = stage ? resource.stagemax : resource.max;
                        break;
                    case "PERCENT":
                        if (stage)
                        {
                            v = resource.stagemax > 0 ? resource.stage / resource.stagemax : 0d;
                        }
                        else
                        {
                            v = resource.max > 0 ? resource.current / resource.max : 0d;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Error finding {0}-{2}: {1}", resourceName, e, valueType);
            }

            return v;
        }

        //public void Add(PartResource resource)
        //{
        //    try
        //    {
        //        ResourceData res = nameResources[resource.info.name];
        //        res.current += (float)resource.amount;
        //        res.max += (float)resource.maxAmount;

        //        var flowmode = resource.info.resourceFlowMode;
        //        if (IsFreeFlow(flowmode))
        //        {
        //            res.stage += (float)resource.amount;
        //            res.stagemax += (float)resource.maxAmount;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        JUtil.LogErrorMessage(this, "Error adding {0}: {1}", resource.info.name, e);
        //    }
        //}

        private class ResourceData
        {
            public string name;

            public float current;
            public float max;
            public float previous;

            public float stage;
            public float stagemax;

            public float density;
            public float delta;

            public int resourceId;
            public ResourceFlowMode flowMode;

            public bool ispropellant;
        }
    }
}
