using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine;

public class FaerieFiresScript : MonoBehaviour
{

    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public ParticleSystem Emitter;
    public KMSelectable Surface;
    public KMSelectable[] FaerieSelectables;

    int moduleId;
    static int moduleIdCounter = 1;
    private bool moduleSolved = false;
    private bool idle = true;
    private bool animating = false;
    private bool placerFinish = false;
    private bool firstStart = true;
    private bool alreadyFalse = false;
    private int currentStage = 0;
    private Color32 transparent = new Color32(255, 255, 255, 25);
    private const string CharList = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private Coroutine[] idleSeq = new Coroutine[6];

    private class FaerieFire
    {
        public string Name;
        public Color32 Color;
        public ParticleSystem Fire;
        public string Sound;
        public string Base36;
        public int Base10;
        public int Order;
    }

    private List<FaerieFire> FaerieFires = new List<FaerieFire>();

    void Start()
    {
        moduleId = moduleIdCounter++;
        var Name = new List<string>() { "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta" };
        var Color = new List<Color32>() { new Color32(225, 50, 50, 255), new Color32(50, 225, 50, 255), new Color32(50, 50, 225, 255), new Color32(225, 225, 50, 255), new Color32(50, 225, 225, 255), new Color32(225, 50, 225, 255) };
        var Sound = new List<string>() { "FaerieGlitter1", "FaerieGlitter2", "FaerieGlitter3", "FaerieGlitter4", "FaerieGlitter5", "FaerieGlitter6" }.Shuffle();
        var Base36 = Enumerable.Range(0, 6).Select(i => string.Format("{0}{1}", Bomb.GetSerialNumber()[(int) char.GetNumericValue(Sound[i][13]) - 1], Name[i][0])).ToList();
        var Order = Enumerable.Range(0, 6).ToList().Shuffle();
        for (int i = 0; i < 6; i++)
            FaerieFires.Add(new FaerieFire { Name = Name[i], Color = Color[i], Fire = new ParticleSystem(), Sound = Sound[i], Base36 = Base36[i], Base10 = Decode(Base36[i]), Order = Order[i] });

        Debug.LogFormat(@"[Faerie Fires #{0}] The following as been generated:", moduleId);
        for (int i = 0; i < FaerieFires.Count; i++)
            Debug.LogFormat(@"[Faerie Fires #{0}] Color: {1} - Pitch: {2} - Base36: {3} - Base10: {4} - Median Difference: {5} - Order: {6}", moduleId, FaerieFires[i].Name, FaerieFires[i].Sound[13], FaerieFires[i].Base36, FaerieFires[i].Base10, MedianDifference(FaerieFires[i].Base10), FaerieFires[i].Order + 1);

        for (int i = 0; i < FaerieSelectables.Length; i++)
            FaerieSelectables[i].gameObject.SetActive(false);

        Surface.OnInteract += delegate
        {
            if (moduleSolved || animating)
                return false;

            if (firstStart)
            {
                firstStart = false;
                StartCoroutine(FirePlacer());
                StartCoroutine(IdleSequenceStarter());
            }
            else
            {
                currentStage = 0;
                if (idle)
                {
                    StartCoroutine(MoveToCornerStarter());
                    idle = false;
                }
                else
                {
                    StartCoroutine(IdleSequenceStarter(fromCorner: true));
                    idle = true;
                }
                return false;
            }
            return false;
        };

        for (int i = 0; i < FaerieSelectables.Length; i++)
            FaerieSelectables[i].OnInteract += FaerieFirePressed(i);
    }

    KMSelectable.OnInteractHandler FaerieFirePressed(int btn)
    {
        return delegate
        {
            if (moduleSolved)
                return false;
            FaerieSelectables[btn].AddInteractionPunch();
            if (currentStage != FaerieFires[btn].Order || (int) Bomb.GetTime() % 10 != MedianDifference(FaerieFires[btn].Base10))
            {
                Module.HandleStrike();
                if (currentStage != FaerieFires[btn].Order)
                    Debug.LogFormat(@"[Faerie Fires #{0}] Stage {1} - Strike! You pressed the {2} faerie house. You should have pressed the {3} faerie house.", moduleId, currentStage + 1, FaerieFires[btn].Name.ToLowerInvariant(), FaerieFires[FaerieFires.IndexOf(order => order.Order == currentStage)].Name.ToLowerInvariant());
                else
                    Debug.LogFormat(@"[Faerie Fires #{0}] Stage {1} - Strike! You pressed the {2} faerie house when the last digit of the timer was {3}. You should have pressed it when it was {4}.", moduleId, currentStage + 1, FaerieFires[btn].Name.ToLowerInvariant(), (int) Bomb.GetTime() % 10, MedianDifference(FaerieFires[btn].Base10));
                return false;
            }
            Audio.PlaySoundAtTransform(FaerieFires[btn].Sound, FaerieSelectables[btn].transform);
            currentStage++;
            animating = true;
            idleSeq[btn] = StartCoroutine(IdleSequence(btn, fromHouse: true));
            if (currentStage == 6)
            {
                moduleSolved = true;
                Module.HandlePass();
                Debug.LogFormat(@"[Faerie Fires #{0}] Module solved!", moduleId);
                StartCoroutine(IdleSequenceStarter());
                StartCoroutine(Solve());
                return false;
            }
            return false;
        };
    }

    private IEnumerator FirePlacer()
    {
        for (var i = 0; i < 6; i++)
        {
            Audio.PlaySoundAtTransform(FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Sound, transform);
            FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Fire = Instantiate(Emitter, transform.Find("Faerie Fires"));
            FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Fire.name = string.Format("{0} Faerie Fire", FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Name);
            var main = FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Fire.main;
            var shape = FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Fire.shape;
            main.startColor = new ParticleSystem.MinMaxGradient(FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Color);
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = transform;
            var t = 200.015f;
            var x = Mathf.Cos(FaerieFires[i].Order * t + t) * 5f;
            var y = Mathf.Sin(FaerieFires[i].Order * t + t) * 5f;
            shape.position = new Vector3(x, y, 10f);
            FaerieFires[FaerieFires.IndexOf(order => order.Order == i)].Fire.Play();
            yield return new WaitForSeconds(0.5f);
        }
        placerFinish = true;
    }

    private IEnumerator IdleSequenceStarter(bool fromCorner = false)
    {
        alreadyFalse = false;
        animating = true;
        yield return new WaitUntil(() => placerFinish);
        for (var i = 0; i < FaerieFires.Count; i++)
        {
            idleSeq[i] = StartCoroutine(IdleSequence(FaerieFires.IndexOf(order => order.Order == i), fromCorner, i == FaerieFires.Count - 1));
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator MoveToCornerStarter()
    {
        animating = true;
        for (var i = 0; i < FaerieFires.Count; i++)
        {
            StopCoroutine(idleSeq[i]);
            StartCoroutine(MoveToCorner(FaerieFires.IndexOf(order => order.Order == i), i == FaerieFires.Count - 1));
            yield return new WaitForSeconds(.5f);
        }
    }

    private IEnumerator MoveToCorner(int pos, bool last = false)
    {
        var duration = 1.5f;
        var elapsed = 0f;

        var shape = FaerieFires[pos].Fire.shape;
        var position = shape.position;

        Audio.PlaySoundAtTransform(FaerieFires[pos].Sound, transform);

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            shape.position = Vector3.Lerp(position, new Vector3(-7.2f, -7.2f + (pos * 2.875f), 1.2f), elapsed / duration);
        }
        if (last)
            animating = false;

        FaerieSelectables[pos].gameObject.SetActive(true);
        FaerieSelectables[pos].gameObject.GetComponentInParent<MeshRenderer>().material.color = new Color32(FaerieFires[pos].Color.r, FaerieFires[pos].Color.g, FaerieFires[pos].Color.b, 100);
    }

    private IEnumerator IdleSequence(int pos, bool fromCorner = false, bool last = false, bool fromHouse = false)
    {
        if (!fromHouse)
        {
            var duration = 1.5f;
            var elapsed = 0f;
            if (fromCorner)
            {
                Audio.PlaySoundAtTransform(FaerieFires[pos].Sound, transform);
                FaerieSelectables[pos].gameObject.SetActive(false);
                FaerieSelectables[pos].gameObject.GetComponentInParent<MeshRenderer>().material.color = transparent;
            }
            var shape = FaerieFires[pos].Fire.shape;
            var light = FaerieFires[pos].Fire.lights;
            var position = shape.position;

            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                shape.position = Vector3.Lerp(position, new Vector3(0, 0, 1.2f), elapsed / duration);
            }
            light.enabled = true;
            var angle = 0f;
            var speed = 2f * Mathf.PI / 6f;
            var radius = .1f;

            while (true)
            {
                if (last && !alreadyFalse)
                {
                    alreadyFalse = true;
                    animating = false;
                }

                yield return null;
                angle -= speed * Time.deltaTime;
                if (radius > 1.1f && light.enabled)
                    light.enabled = false;
                if (radius < 5f)
                    radius += Time.deltaTime;
                angle %= 360f;
                var x = Mathf.Cos(angle) * radius;
                var y = Mathf.Sin(angle) * radius;
                shape.position = new Vector3(x, y, 1.5f);
            }
        }
        else
        {
            var duration = 1.5f;
            var elapsed = 0f;
            FaerieSelectables[pos].gameObject.SetActive(false);
            FaerieSelectables[pos].gameObject.GetComponentInParent<MeshRenderer>().material.color = transparent;
            var shape = FaerieFires[pos].Fire.shape;
            var light = FaerieFires[pos].Fire.lights;
            var position = shape.position;

            var t = 200.015f;
            var x = Mathf.Cos(FaerieFires[pos].Order * t + t) * 2f;
            var y = Mathf.Sin(FaerieFires[pos].Order * t + t) * 2f;

            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                shape.position = Vector3.Lerp(position, new Vector3(x, y, 1.2f), elapsed / duration);
            }
            animating = false;
        }

    }

    private IEnumerator Solve()
    {
        var Sounds = new List<string>() { "FaerieGlitter1", "FaerieGlitter2", "FaerieGlitter3", "FaerieGlitter4", "FaerieGlitter5", "FaerieGlitter6" };
        var wait = 0.4f;
        yield return new WaitForSeconds(1f);
        Audio.PlaySoundAtTransform(Sounds[0], transform);
        Audio.PlaySoundAtTransform(Sounds[2], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[2], transform);
        Audio.PlaySoundAtTransform(Sounds[5], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[1], transform);
        Audio.PlaySoundAtTransform(Sounds[3], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[2], transform);
        Audio.PlaySoundAtTransform(Sounds[4], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[2], transform);
        Audio.PlaySoundAtTransform(Sounds[5], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[1], transform);
        Audio.PlaySoundAtTransform(Sounds[3], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[3], transform);
        Audio.PlaySoundAtTransform(Sounds[5], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[2], transform);
        Audio.PlaySoundAtTransform(Sounds[4], transform);
        yield return new WaitForSeconds(wait);
        Audio.PlaySoundAtTransform(Sounds[0], transform);
        yield return new WaitForSeconds(0.1f);
        Audio.PlaySoundAtTransform(Sounds[2], transform);
        yield return new WaitForSeconds(0.1f);
        Audio.PlaySoundAtTransform(Sounds[5], transform);
    }

    private int Decode(string input)
    {
        var reversed = input.ToUpperInvariant().Reverse();
        int result = 0;
        int pos = 0;
        foreach (char c in reversed)
        {
            result += CharList.IndexOf(c) * (int) Math.Pow(36, pos);
            pos++;
        }
        return result;
    }

    private static int MedianDifference(int number)
    {
        if (number > 999)
        {
            var resultstr = number.ToString();
            var n1 = (int) resultstr[0];
            var n2 = (int) resultstr[1];
            var n3 = (int) resultstr[2];
            var n4 = (int) resultstr[3];
            return Math.Abs(Math.Abs(Math.Abs(n1 - n2) - Math.Abs(n2 - n3)) - Math.Abs(Math.Abs(n2 - n3) - Math.Abs(n3 - n4)));
        }
        else if (number > 99)
        {
            var resultstr = number.ToString();
            var n1 = (int) resultstr[0];
            var n2 = (int) resultstr[1];
            var n3 = (int) resultstr[2];
            return Math.Abs(Math.Abs(n1 - n2) - Math.Abs(n2 - n3));
        }
        else if (number > 9)
        {
            var resultstr = number.ToString();
            var n1 = (int) resultstr[0];
            var n2 = (int) resultstr[1];
            return Math.Abs(n1 - n2);
        }
        else
            return number;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} touch [Click the surface (Twice if the module hasn't started yet)] | !{0} 13-25-36[Click the top house at 3 seconds, the second house at 5 seconds and the third hourse at 6 seconds. Also touches the screen if the faeries aren't at home.]";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        if (moduleSolved)
        {
            yield return "sendtochaterror The module is already solved.";
            yield break;
        }
        else if ((m = Regex.Match(command, @"^\s*([0123456789-]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            yield return null;

            var cell = m.Groups[0].Value.Split('-');

            for (int i = 0; i < cell.Length; i++)
            {
                if (Regex.IsMatch(cell[i], @"^\s*[123456][0123456789]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    continue;
                else
                {
                    yield return "sendtochaterror Incorrect syntax.";
                    yield break;
                }
            }

            var touch = false;
            if (!placerFinish)
            {
                Surface.OnInteract();
                while (animating)
                {
                    yield return true;
                    yield return new WaitForSeconds(.05f);
                }
            }
            for (int i = 0; i < FaerieSelectables.Length; i++)
            {
                if (!FaerieSelectables[i].gameObject.activeSelf)
                {
                    touch = true;
                }
            }
            if (touch)
            {
                Surface.OnInteract();
                for (int i = 0; i < FaerieSelectables.Length; i++)
                {
                    if (!FaerieSelectables[i].gameObject.activeSelf)
                        while (!FaerieSelectables[i].gameObject.activeSelf)
                        {
                            yield return true;
                            yield return new WaitForSeconds(.05f);
                        }
                }
            }

            for (int i = 0; i < cell.Length; i++)
            {
                var house = int.Parse(cell[i].Substring(0, 1));
                var time = int.Parse(cell[i].Substring(1, 1));
                while ((int) Bomb.GetTime() % 10 != time)
                {
                    yield return true;
                    yield return new WaitForSeconds(.05f);
                }
                FaerieSelectables[house-1].OnInteract();
            }
            yield break;
        }
        else if (Regex.IsMatch(command, @"^\s*touch\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (!placerFinish)
            {
                Surface.OnInteract();
                while (animating)
                {
                    yield return true;
                    yield return new WaitForSeconds(.05f);
                }
            }
            Surface.OnInteract();
            yield break;
        }
        else
        {
            yield return "sendtochaterror Invalid Command";
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat(@"[Faerie Fires #{0}] Module was force solved by TP", moduleId);
        var touch = false;
        if (!placerFinish)
        {
            Surface.OnInteract();
            while (animating)
            {
                yield return true;
                yield return new WaitForSeconds(.05f);
            }
        }
        for (int i = 0; i < FaerieSelectables.Length; i++)
        {
            if (!FaerieSelectables[i].gameObject.activeSelf)
            {
                touch = true;
            }
        }
        if (touch)
        {
            Surface.OnInteract();
            for (int i = 0; i < FaerieSelectables.Length; i++)
            {
                if (!FaerieSelectables[i].gameObject.activeSelf)
                    while (!FaerieSelectables[i].gameObject.activeSelf)
                    {
                        yield return true;
                        yield return new WaitForSeconds(.05f);
                    }
            }
        }
        for (int i = 0; i < FaerieFires.Count; i++)
        {
            while ((int) Bomb.GetTime() % 10 != MedianDifference(FaerieFires[FaerieFires.IndexOf((b) => b.Order == i)].Base10))
            {
                yield return true;
                yield return new WaitForSeconds(.05f);
            }
            FaerieSelectables[FaerieFires.IndexOf((b) => b.Order == i)].OnInteract();
        }
        yield break;
    }
}