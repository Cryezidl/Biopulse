using System;
using System.Text;
using Biopulse.Emg;
using UnityEngine;

namespace Biopulse.Calibration
{
    [Serializable]
    public sealed class CalibrationProfile
    {
        public int schema = 1, version = 1;
        public string sourceId, representation, units, settings, createdUtc;
        public string method = "rest-median_3-pulses-median-p75-v1";
        public bool verified;
        public float flexBaseline, flexReference, extendBaseline, extendReference;
        public EmgCalibration ToCalibration() => new EmgCalibration(sourceId, flexBaseline, flexReference, extendBaseline, extendReference, version);
        public static CalibrationProfile Create(EmgSourceInfo info, string settings, EmgCalibration result)
        {
            return new CalibrationProfile { sourceId = info.Id, representation = info.Representation.ToString(), units = info.Units,
                settings = settings, createdUtc = DateTime.UtcNow.ToString("O"), version = result.Version,
                flexBaseline = result.FlexBaseline, flexReference = result.FlexReference,
                extendBaseline = result.ExtendBaseline, extendReference = result.ExtendReference };
        }
        public bool Matches(EmgSourceInfo info, string signature) => schema == 1 && verified && sourceId == info.Id &&
            representation == info.Representation.ToString() && units == info.Units && settings == signature &&
            method == "rest-median_3-pulses-median-p75-v1";
    }
    public static class ProfileStore
    {
        private static string Key(string sourceId) => "Biopulse.Calibration.v1." + Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceId));
        public static void Save(CalibrationProfile profile)
        {
            if (!profile.verified) throw new InvalidOperationException("Validate the profile before saving.");
            profile.ToCalibration(); PlayerPrefs.SetString(Key(profile.sourceId), JsonUtility.ToJson(profile)); PlayerPrefs.Save();
        }
        public static bool TryLoad(EmgSourceInfo info, string settings, out CalibrationProfile profile)
        {
            profile = null;
            try
            {
                string json = PlayerPrefs.GetString(Key(info.Id), "");
                if (json.Length == 0) return false;
                var candidate = JsonUtility.FromJson<CalibrationProfile>(json);
                if (candidate == null || !candidate.Matches(info, settings)) return false;
                candidate.ToCalibration(); profile = candidate; return true;
            }
            catch (ArgumentException) { return false; }
        }
        public static void Delete(string sourceId) { PlayerPrefs.DeleteKey(Key(sourceId)); PlayerPrefs.Save(); }
    }
}
