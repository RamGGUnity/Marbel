using UnityEngine;
using System;
using System.Collections.Generic;

namespace Marble
{
    [Serializable]
    public struct MarbleCountryDefaults
    {
        public string name;
        public string shortName;

        public MarbleCountryDefaults(string name, string shortName)
        {
            this.name = name;
            this.shortName = shortName;
        }
    }

    [Serializable]
    public struct MarbleSpawnData
    {
        public MarbleCountryDefaults country;
        public Material material;
    }

    public class MarbleSpawnPoints : MonoBehaviour
    {
        [Header("Spawn Points (child transforms)")]
        [SerializeField] private List<Transform> spawnTransforms = new List<Transform>();

        [Header("Marble Data")]
        [SerializeField]
        public List<MarbleSpawnData> marbles = new List<MarbleSpawnData>(16);

        public static readonly MarbleCountryDefaults[] DefaultCountries = new MarbleCountryDefaults[]
        {
            new MarbleCountryDefaults("Argentina", "ARG"),
            new MarbleCountryDefaults("Brazil", "BRA"),
            new MarbleCountryDefaults("Canada", "CAN"),
            new MarbleCountryDefaults("China", "CHN"),
            new MarbleCountryDefaults("Denmark", "DEN"),
            new MarbleCountryDefaults("England", "ENG"),
            new MarbleCountryDefaults("Estonia", "EST"),
            new MarbleCountryDefaults("France", "FRA"),
            new MarbleCountryDefaults("Germany", "GER"),
            new MarbleCountryDefaults("India", "IND"),
            new MarbleCountryDefaults("Italy", "ITA"),
            new MarbleCountryDefaults("Japan", "JPN"),
            new MarbleCountryDefaults("Russia", "RUS"),
            new MarbleCountryDefaults("Saudi Arabia", "KSA"),
            new MarbleCountryDefaults("UAE", "UAE"),
            new MarbleCountryDefaults("USA", "USA")
        };

        public Vector3 GetSpawnPosition(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < spawnTransforms.Count && spawnTransforms[slotIndex] != null)
            {
                return spawnTransforms[slotIndex].position;
            }

            Debug.LogWarning($"[SPAWN] No spawn transform for slot {slotIndex}, using parent position");
            return transform.position;
        }

        public Quaternion GetSpawnRotation(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < spawnTransforms.Count && spawnTransforms[slotIndex] != null)
            {
                return spawnTransforms[slotIndex].rotation;
            }

            return transform.rotation;
        }

        public Material GetMaterial(int marbleId)
        {
            if (marbleId < 0 || marbleId >= marbles.Count)
            {
                return null;
            }

            return marbles[marbleId].material;
        }

        public string GetName(int marbleId)
        {
            if (marbleId < 0 || marbleId >= marbles.Count)
            {
                return marbleId < DefaultCountries.Length ? DefaultCountries[marbleId].name : $"Marble {marbleId}";
            }

            return string.IsNullOrEmpty(marbles[marbleId].country.name)
                ? (marbleId < DefaultCountries.Length ? DefaultCountries[marbleId].name : $"Marble {marbleId}")
                : marbles[marbleId].country.name;
        }

        public string GetShortName(int marbleId)
        {
            if (marbleId < 0 || marbleId >= marbles.Count)
            {
                return marbleId < DefaultCountries.Length ? DefaultCountries[marbleId].shortName : $"M{marbleId}";
            }

            return string.IsNullOrEmpty(marbles[marbleId].country.shortName)
                ? (marbleId < DefaultCountries.Length ? DefaultCountries[marbleId].shortName : $"M{marbleId}")
                : marbles[marbleId].country.shortName;
        }
    }
}
