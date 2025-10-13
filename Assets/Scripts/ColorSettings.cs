// ColorSettings.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ColorSettings", menuName = "Hexagon Game/Color Settings")]
public class ColorSettings : ScriptableObject
{
    public List<Color> availableColors = new List<Color>();
    public Color hiddenColor = Color.black;
    public int piecesPerColor = 2;
}