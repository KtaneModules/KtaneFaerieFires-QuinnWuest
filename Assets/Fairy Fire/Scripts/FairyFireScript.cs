using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using KModkit;
using UnityEngine;

public class FairyFireScript : MonoBehaviour
{

    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public ParticleSystem Emitter;
    public KMSelectable Surface;
    public AudioSource FairyMove;

    ParticleSystem[] fairyFires = new ParticleSystem[6];

    int moduleId;
    static int moduleIdCounter = 1;
    bool moduleSolved = false;
    bool clockWise = true;
    bool swapped = false;
    bool idle = true;
    bool animating = false;
    bool placerFinish = false;

    class Colors
    {
        public string Name;
        public Color32 Color;
        public int Value;
    }

    List<Colors> colors = new List<Colors>()
    {
        new Colors{Name = "Red", Color = new Color32(225, 50, 50, 255) },
        new Colors{Name = "Green", Color = new Color32(50, 225, 50, 255) },
        new Colors{Name = "Blue", Color = new Color32(50, 50, 225, 255) },
        new Colors{Name = "Yellow", Color = new Color32(225, 225, 50, 255) },
        new Colors{Name = "Cyan", Color = new Color32(50, 225, 225, 255) },
        new Colors{Name = "Magenta", Color = new Color32(225, 50, 225, 255) },
    };
    private Coroutine bigSwitch;

    Coroutine[] IdleSeq = new Coroutine[6];


    void Start()
    {

        Surface.OnInteract += delegate
        {
            if (moduleSolved || animating)
                return false;
            if (idle)
            {
                StartCoroutine(MoveToCornerStarter());
                idle = false;
            }
            else
            {
                StartCoroutine(IdleSequenceStarter());
                idle = true;
            }
            return false;
        };

        var sN = Bomb.GetSerialNumber();
        var cN = new[] { 'R', 'G', 'B', 'Y', 'C', 'M' };

        for (var i = 0; i < sN.Length; i++)
        {
            var t = new List<char>();
            t.Add(sN[i]);
            t.Add(cN[i]);
            t.Sort();
            colors[i].Value = FromBase36(t.Join(""));
        }

        colors.Shuffle();

        var sortedColors = colors.ToList();
        sortedColors.Sort((s1, s2) => s1.Value.CompareTo(s2.Value));

        Debug.LogFormat(@"[Fairy Fire #{0}] Colors values in order are (starting with lowest value): {1}", moduleId, sortedColors.Select(c => string.Format("{0} - {1}", c.Name, c.Value)).Join(" | "));


        moduleId = moduleIdCounter++;
        Module.OnActivate += delegate
        {
            animating = true;
            StartCoroutine(FirePlacer());
            StartCoroutine(IdleSequenceStarter());
        };

    }

    private IEnumerator FirePlacer()
    {
        for (var i = 0; i < 6; i++)
        {
            Audio.PlaySoundAtTransform("FairyGlitter", transform);
            fairyFires[i] = Instantiate(Emitter, transform.Find("Fairy Fires"));
            fairyFires[i].name = string.Format("{0} Fairy Fire", colors[i].Name);
            var main = fairyFires[i].main;
            var shape = fairyFires[i].shape;
            main.startColor = new ParticleSystem.MinMaxGradient(colors[i].Color);
            var t = 200f;
            var x = Mathf.Cos(i * t + t) * 5f;
            var y = Mathf.Sin(i * t + t) * 5f;
            shape.position = new Vector3(x, y, 10f);
            fairyFires[i].Play();
            yield return new WaitForSeconds(0.5f);
        }
        placerFinish = true;
    }

    private IEnumerator MoveToCornerStarter()
    {
        animating = true;
        for (var i = 0; i < fairyFires.Length; i++)
        {
            StopCoroutine(IdleSeq[i]);
            StartCoroutine(MoveToCorner(i));
            yield return new WaitForSeconds(.5f);
        }
        StopCoroutine(bigSwitch);
    }

    private IEnumerator MoveToCorner(int pos)
    {
        var duration = 1.5f;
        var elapsed = 0f;

        var shape = fairyFires[pos].shape;
        var position = shape.position;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            shape.position = Vector3.Lerp(position, new Vector3(-6f, pos * 2.4f - 6, 1.2f), elapsed / duration);
        }
        if (pos == 5)
        {
            animating = false;
            FairyMove.Stop();
        }
    }

    private IEnumerator IdleSequenceStarter()
    {
        yield return new WaitUntil(() => placerFinish);
        FairyMove.Play();
        for (var i = 0; i < fairyFires.Length; i++)
        {
            IdleSeq[i] = StartCoroutine(IdleSequence(i));
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator IdleSequence(int pos)
    {
        var duration = 1.5f;
        var elapsed = 0f;

        var shape = fairyFires[pos].shape;
        var light = fairyFires[pos].lights;
        var position = shape.position;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            shape.position = Vector3.Lerp(position, new Vector3(0, 0, 1.2f), elapsed / duration);
        }
        var angle = 0f;
        var speed = 2f * Mathf.PI / 6f;
        var radius = .1f;

        while (true)
        {
            if (pos == 5)
                animating = false;
            yield return null;
            if (clockWise)
                angle += speed * Time.deltaTime;
            else
                angle -= speed * Time.deltaTime;
            if (radius > 1f && light.enabled)
                light.enabled = false;
            if (radius < 5f)
                radius += Time.deltaTime;
            angle %= 360f;
            var x = Mathf.Cos(angle) * radius;
            var y = Mathf.Sin(angle) * radius;
            shape.position = new Vector3(x, y, 1.5f);
        }

    }

    static int FromBase36(char ch)
    {
        return ch >= 'A' && ch <= 'Z' ? ch - 'A' + 10 : ch - '0';
    }
    static int FromBase36(string str)
    {
        return str.Aggregate(0, (p, n) => p * 36 + FromBase36(n));
    }

}
