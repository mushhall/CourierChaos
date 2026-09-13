using UnityEngine;
using System.Collections.Generic;

// Persistent top-5 high score board, saved to disk with PlayerPrefs.
public static class HighScoreTable
{
    public const int MaxEntries = 5;

    public struct Entry { public string name; public int score; }

    public static List<Entry> Load()
    {
        List<Entry> list = new List<Entry>();
        for (int i = 0; i < MaxEntries; i++)
        {
            if (!PlayerPrefs.HasKey("hs_score_" + i)) continue;
            Entry e;
            e.score = PlayerPrefs.GetInt("hs_score_" + i, 0);
            e.name = PlayerPrefs.GetString("hs_name_" + i, "---");
            list.Add(e);
        }
        list.Sort((a, b) => b.score.CompareTo(a.score));
        return list;
    }

    // Would this score make the board?
    public static bool Qualifies(int score)
    {
        if (score <= 0) return false;
        List<Entry> list = Load();
        if (list.Count < MaxEntries) return true;
        return score > list[list.Count - 1].score;
    }

    public static void Add(string name, int score)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "PLAYER";
        name = name.Trim();
        if (name.Length > 12) name = name.Substring(0, 12);

        List<Entry> list = Load();
        list.Add(new Entry { name = name, score = score });
        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);

        for (int i = 0; i < list.Count; i++)
        {
            PlayerPrefs.SetInt("hs_score_" + i, list[i].score);
            PlayerPrefs.SetString("hs_name_" + i, list[i].name);
        }
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        for (int i = 0; i < MaxEntries; i++)
        {
            PlayerPrefs.DeleteKey("hs_score_" + i);
            PlayerPrefs.DeleteKey("hs_name_" + i);
        }
        PlayerPrefs.Save();
    }
}