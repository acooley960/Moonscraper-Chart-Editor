﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SongEditAdd : SongEditCommand
{
    List<SongObject> overwrittenSongObjects = new List<SongObject>();

    public SongEditAdd(IList<SongObject> songObjects) : base(songObjects)
    {
        foreach (SongObject songObject in songObjects)
        {
            Debug.Assert(songObject.song == null, "Must add a new song object!");
        }
    }

    public SongEditAdd(SongObject songObject) : base(songObject)
    {
        Debug.Assert(songObject.song == null, "Must add a new song object!");
    }

    public override void InvokeSongEditCommand()
    {
        if (hasValidatedSongObjects)
        {
            ApplyPostValidatedAction(validatedSongObjects, overwrittenSongObjects);
        }
        else
        {
            ApplyActionAndFillValidation(GameSettings.extendedSustainsEnabled);
            songObjects.Clear();
            for (int i = validatedSongObjects.Count - 1; i >= 0; --i)
            {
                SongObject so = validatedSongObjects[i];
                if (so.song == null)    
                {
                    // Song object was probably removed during the initial add process, thus was never added at all
                    validatedSongObjects.RemoveAt(i);
                    overwrittenSongObjects.Remove(so);
                }
                else
                    validatedSongObjects[i] = so.Clone();
            }

            hasValidatedSongObjects = true;
        }
    }

    public override void RevokeSongEditCommand()
    {
        Debug.Assert(hasValidatedSongObjects, "Trying to revoke add action which has not made it's initial validation pass!");
        ApplyPostValidatedAction(overwrittenSongObjects, validatedSongObjects);
    }

    void ApplyPostValidatedAction(IList<SongObject> songObjectsToAdd, IList<SongObject> songObjectsToDelete)
    {
        SongEditDelete.ApplyAction(songObjectsToDelete, null);  // Overwrite can be null for special case with song edit add, as corrections can mess SEA up

        foreach (SongObject songObject in songObjectsToAdd)
        {
            ApplyPostValidatedAction(songObject);
        }
    }

    public static void ApplyAction(IList<SongObject> songObjects, IList<SongObject> overwriteList, bool extendedSustainsEnabled)
    {
        List<SongObject> dummy = new List<SongObject>();
        foreach (SongObject songObject in songObjects)
        {
            ApplyAction(songObject, overwriteList, extendedSustainsEnabled, dummy);
        }
    }

    void ApplyActionAndFillValidation(bool extendedSustainsEnabled)
    {
        foreach (SongObject songObject in songObjects)
        {
            ApplyAction(songObject, overwrittenSongObjects, extendedSustainsEnabled, validatedSongObjects);
        }
    }

    static void ApplyAction(SongObject songObject, IList<SongObject> overwriteList, bool extendedSustainsEnabled, List<SongObject> validatedObjects)
    {
        SongObject validatedSo = null;

        switch (songObject.classID)
        {
            case ((int)SongObject.ID.Note):
                AddNote((Note)songObject, overwriteList, extendedSustainsEnabled, validatedObjects);
                break;

            case ((int)SongObject.ID.Starpower):
                validatedSo = AddStarpower((Starpower)songObject, overwriteList, validatedObjects);
                break;

            case ((int)SongObject.ID.ChartEvent):
                validatedSo = AddChartEvent((ChartEvent)songObject, overwriteList);
                break;

            case ((int)SongObject.ID.BPM):
                validatedSo = AddBPM((BPM)songObject, overwriteList);
                break;

            case ((int)SongObject.ID.Section):
                validatedSo = AddSection((Section)songObject, overwriteList);
                break;

            case ((int)SongObject.ID.TimeSignature):
                validatedSo = AddTimeSignature((TimeSignature)songObject, overwriteList);
                break;

            case ((int)SongObject.ID.Event):
                validatedSo = AddEvent((Event)songObject, overwriteList);
                break;

            default:
                Debug.LogError("Unhandled songobject!");
                break;
        }

        if (validatedSo != null)
            validatedObjects.Add(validatedSo);
    }

    public static void ApplyPostValidatedAction(SongObject songObject)
    {
        switch (songObject.classID)
        {
            case ((int)SongObject.ID.Note):
                {
                    Note note = songObject as Note;
                    ChartEditor editor = ChartEditor.Instance;
                    Chart chart = editor.currentChart;
                    chart.Add(note);

                    foreach (Note chordNote in note.chord)
                    {
                        if (chordNote.controller)
                            chordNote.controller.SetDirty();
                    }

                    Note next = note.nextSeperateNote;
                    if (next != null)
                    {
                        foreach(Note chordNote in next.chord)
                        {
                            if (chordNote.controller)
                                chordNote.controller.SetDirty();
                        }
                    }
                }
                break;
            case ((int)SongObject.ID.Starpower):
            case ((int)SongObject.ID.ChartEvent):
                {
                    ChartEditor editor = ChartEditor.Instance;
                    Chart chart = editor.currentChart;
                    chart.Add(songObject as ChartObject);
                }
                break;

            case ((int)SongObject.ID.BPM):
            case ((int)SongObject.ID.TimeSignature):
                {
                    ChartEditor editor = ChartEditor.Instance;
                    Song song = editor.currentSong;
                    song.Add(songObject as SyncTrack);
                }
                break;

            case ((int)SongObject.ID.Section):          
            case ((int)SongObject.ID.Event):
                {
                    ChartEditor editor = ChartEditor.Instance;
                    Song song = editor.currentSong;
                    song.Add(songObject as Event);
                }
                break;

            default:
                Debug.LogError("Unhandled songobject!");
                break;
        }
    }

    #region Object specific add functions

    static void TryRecordOverwrite<T>(T songObject, IList<T> searchObjects, IList<SongObject> overwrittenObjects) where T : SongObject
    {
        if (overwrittenObjects == null)
            return;

        ChartEditor editor = ChartEditor.Instance;
        int overwriteIndex = SongObjectHelper.FindObjectPosition(songObject, searchObjects);

        if (overwriteIndex != SongObjectHelper.NOTFOUND)
        {
            overwrittenObjects.Add(searchObjects[overwriteIndex].Clone());
        }
    }

    static void TryRecordOverwrite(Starpower songObject, IList<ChartObject> searchObjects, IList<SongObject> overwrittenObjects)
    {
        if (overwrittenObjects == null)
            return;

        ChartEditor editor = ChartEditor.Instance;
        int overwriteIndex = SongObjectHelper.FindObjectPosition(songObject, searchObjects);

        if (overwriteIndex != SongObjectHelper.NOTFOUND)
        {
            overwrittenObjects.Add(searchObjects[overwriteIndex].Clone());
            SetNotesDirty(songObject, searchObjects);
        }
    }

    static void AddNote(Note note, IList<SongObject> overwrittenList, bool extendedSustainsEnabled, List<SongObject> validatedNotes)
    {
        ChartEditor editor = ChartEditor.Instance;
        Chart chart = editor.currentChart;
        Song song = editor.currentSong;

        Note noteToAdd = new Note(note);

        NoteFunctions.PerformPreChartInsertCorrections(noteToAdd, chart, validatedNotes, overwrittenList, extendedSustainsEnabled);
        chart.Add(noteToAdd, false);
        NoteFunctions.PerformPostChartInsertCorrections(noteToAdd, validatedNotes, overwrittenList, extendedSustainsEnabled);
        
        // Queue visual refresh
        {
            foreach (Note chordNote in noteToAdd.chord)
            {
                if (chordNote.controller)
                    chordNote.controller.SetDirty();
            }

            Note next = noteToAdd.nextSeperateNote;
            if (next != null)
            {
                foreach (Note chordNote in next.chord)
                {
                    if (chordNote.controller)
                        chordNote.controller.SetDirty();
                }
            }
        }

        if (!validatedNotes.Contains(noteToAdd))
            validatedNotes.Add(noteToAdd);
    }

    static Starpower AddStarpower(Starpower sp, IList<SongObject> overwrittenList, IList<SongObject> validatedList)
    {
        ChartEditor editor = ChartEditor.Instance;
        TryRecordOverwrite(sp, editor.currentChart.chartObjects, overwrittenList);

        CapPrevAndNextPreInsert(sp, editor.currentChart, overwrittenList, validatedList);

        Starpower spToAdd = new Starpower(sp);
        editor.currentChart.Add(spToAdd, false);
        Debug.Log("Added new starpower");

        SetNotesDirty(spToAdd, editor.currentChart.chartObjects);

        return spToAdd;
    }

    static ChartEvent AddChartEvent(ChartEvent chartEvent, IList<SongObject> overwrittenList)
    {      
        ChartEditor editor = ChartEditor.Instance;
        TryRecordOverwrite(chartEvent, editor.currentChart.chartObjects, overwrittenList);

        ChartEvent eventToAdd = new ChartEvent(chartEvent);

        editor.currentChart.Add(eventToAdd, false);
        Debug.Log("Added new chart event");

        return eventToAdd;
    }

    static BPM AddBPM(BPM bpm, IList<SongObject> overwrittenList)
    {
        ChartEditor editor = ChartEditor.Instance;
        TryRecordOverwrite(bpm, editor.currentSong.bpms, overwrittenList);

        BPM bpmToAdd = new BPM(bpm);
        editor.currentSong.Add(bpmToAdd, false);
        Debug.Log("Added new bpm");

        if (bpmToAdd.anchor != null)
        {
            bpmToAdd.anchor = bpmToAdd.song.LiveTickToTime(bpmToAdd.tick, bpmToAdd.song.resolution);
        }

        ChartEditor.Instance.songObjectPoolManager.SetAllPoolsDirty();

        return bpmToAdd;
    }

    static TimeSignature AddTimeSignature(TimeSignature timeSignature, IList<SongObject> overwrittenList)
    {
        ChartEditor editor = ChartEditor.Instance;
        TryRecordOverwrite(timeSignature, editor.currentSong.timeSignatures, overwrittenList);

        TimeSignature tsToAdd = new TimeSignature(timeSignature);
        editor.currentSong.Add(tsToAdd, false);
        Debug.Log("Added new timesignature");

        return tsToAdd;
    }

    static Event AddEvent(Event songEvent, IList<SongObject> overwrittenList)
    {
        ChartEditor editor = ChartEditor.Instance;
        TryRecordOverwrite(songEvent, editor.currentSong.events, overwrittenList);

        Event eventToAdd = new Event(songEvent);
        editor.currentSong.Add(eventToAdd, false);

        Debug.Log("Added new song event");

        return eventToAdd;
    }

    static Section AddSection(Section section, IList<SongObject> overwrittenList)
    {
        ChartEditor editor = ChartEditor.Instance;
        TryRecordOverwrite(section, editor.currentSong.sections, overwrittenList);

        Section sectionToAdd = new Section(section);
        editor.currentSong.Add(sectionToAdd, false);

        Debug.Log("Added new section");

        return sectionToAdd;
    }

    #endregion

    #region Starpower Helper Functions

    public static void SetNotesDirty(Starpower sp, IList<ChartObject> notes)
    {
        int start, length;
        SongObjectHelper.GetRange(notes, sp.tick, sp.tick + sp.length, out start, out length);

        for (int i = start; i < start + length; ++i)
        {
            if (notes[i].classID == (int)SongObject.ID.Note && notes[i].controller)
                notes[i].controller.SetDirty();
        }
    }

    static void CapPrevAndNextPreInsert(Starpower sp, Chart chart, IList<SongObject> overwrittenList, IList<SongObject> validatedList)
    {
        int arrayPos = SongObjectHelper.FindClosestPosition(sp, chart.chartObjects);

        if (arrayPos != SongObjectHelper.NOTFOUND)       // Found an object that matches
        {
            Starpower previousSp = null;
            Starpower nextSp = null;

            bool currentArrayPosIsStarpower = chart.chartObjects[arrayPos] as Starpower == null;

            // Find the previous starpower
            {
                int previousSpIndex = currentArrayPosIsStarpower ? arrayPos - 1 : arrayPos;
                while (previousSpIndex >= 0 && chart.chartObjects[previousSpIndex].tick < sp.tick)
                {
                    Starpower maybeSp = chart.chartObjects[previousSpIndex] as Starpower;
                    if (maybeSp == null)
                    {
                        --previousSpIndex;
                    }
                    else
                    {
                        previousSp = maybeSp;
                        break;
                    }
                }
            }

            // Find the next starpower
            {
                int nextSpIndex = currentArrayPosIsStarpower ? arrayPos + 1 : arrayPos;
                while (nextSpIndex < chart.chartObjects.Count && chart.chartObjects[nextSpIndex].tick > sp.tick)
                {
                    Starpower maybeSp = chart.chartObjects[nextSpIndex] as Starpower;
                    if (maybeSp == null)
                    {
                        ++nextSpIndex;
                    }
                    else
                    {
                        nextSp = maybeSp;
                        break;
                    }
                }
            }

            if (previousSp != null)
            {
                // Cap previous sp
                if (previousSp.tick + previousSp.length > sp.tick)
                {
                    previousSp.Delete();
                    overwrittenList.Add(previousSp.Clone());

                    uint newLength = sp.tick - previousSp.tick;
                    Starpower newSp = new Starpower(previousSp.tick, newLength);
                    chart.Add(newSp);
                    validatedList.Add(newSp);
                }
            }

            if (nextSp != null)
            {
                // Cap self
                if (sp.tick + sp.length > nextSp.tick)
                {
                    sp.length = nextSp.tick - sp.tick;
                }
            }
        }
    }

    #endregion
}
