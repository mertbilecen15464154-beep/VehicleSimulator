using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;


public class VehicleScreenManager : MonoBehaviour
{
    [Header("Hardware Sync (BNG)")]
    public BNG.HingeHelper volumeKnob;
    public BNG.HingeHelper radioKnob;

    [Header("Physical Buttons (BNG)")]
    public BNG.Button playPauseBNGButton;
    public BNG.Button nextBNGButton;
    public BNG.Button previousBNGButton;
    public BNG.Button mixBNGButton;

    [Header("Audio Setup")]
    public AudioSource radioSource;
    public List<AudioClip> playlist;

    [Header("UI Controls - Volume Slider")]
    public Slider volumeSlider;

    [Header("UI Icons for Physical Buttons")]
    // Not: Fiziksel butonlarýn üzerinde birer Image/SpriteRenderer varsa bunlarý güncelleyebiliriz
    public Image playPauseDisplayImage;
    public Image mixDisplayImage;
    public Sprite playIcon;
    public Sprite pauseIcon;
    public Sprite mixOnIcon;
    public Sprite mixOffIcon;

    [Header("UI Volume Visuals")]
    public GameObject[] volumeLevels;

    [Header("UI Setup - Header")]
    public Text timeText;
    public Text stationText;

    private int _currentTrackIndex = 0;
    private bool _isPlaying = false;
    private bool _isMixEnabled = false;
    private bool _isUpdatingFromKnob = false;

    void Start()
    {
        if (radioSource == null || playlist.Count == 0) return;

        // BNG Fiziksel Buton Dinleyicileri
        if (playPauseBNGButton) playPauseBNGButton.onButtonDown.AddListener(TogglePlayPause);
        if (nextBNGButton) nextBNGButton.onButtonDown.AddListener(NextTrack);
        if (previousBNGButton) previousBNGButton.onButtonDown.AddListener(PreviousTrack);
        if (mixBNGButton) mixBNGButton.onButtonDown.AddListener(ToggleMix);

        // Knob ve Slider Dinleyicileri
        if (radioKnob) radioKnob.onValueChange.AddListener(OnRadioKnobRotated);
        if (volumeKnob) volumeKnob.onValueChange.AddListener(OnVolumeKnobRotated);
        if (volumeSlider) volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);

        // Baþlangýç Ayarlarý
        radioSource.Stop();
        _isPlaying = false;

        UpdatePlayPauseUI();
        UpdateMixUI();
        SelectTrack(0, false);

        if (volumeSlider) OnVolumeSliderChanged(50f);
    }

    void Update()
    {
        if (timeText) timeText.text = DateTime.Now.ToString("HH:mm");

        // Otomatik sonraki parçaya geçiþ
        if (_isPlaying && !radioSource.isPlaying && radioSource.time == 0)
        {
            NextTrack();
        }
    }

    public void TogglePlayPause()
    {
        _isPlaying = !_isPlaying;
        if (_isPlaying)
        {
            if (radioSource.clip == null) SelectTrack(_currentTrackIndex, true);
            else radioSource.Play();
        }
        else radioSource.Pause();

        UpdatePlayPauseUI();
    }

    public void ToggleMix()
    {
        _isMixEnabled = !_isMixEnabled;
        UpdateMixUI();
    }

    void UpdatePlayPauseUI()
    {
        // Fiziksel butonun üzerindeki veya ekrandaki ikonu günceller
        if (playPauseDisplayImage && playIcon && pauseIcon)
            playPauseDisplayImage.sprite = _isPlaying ? pauseIcon : playIcon;
    }

    void UpdateMixUI()
    {
        if (mixDisplayImage && mixOnIcon && mixOffIcon)
            mixDisplayImage.sprite = _isMixEnabled ? mixOnIcon : mixOffIcon;
    }

    public void NextTrack()
    {
        int nextIndex = _isMixEnabled ? UnityEngine.Random.Range(0, playlist.Count) : (_currentTrackIndex + 1) % playlist.Count;
        SelectTrack(nextIndex, _isPlaying);
    }

    public void PreviousTrack()
    {
        int prevIndex = _isMixEnabled ? UnityEngine.Random.Range(0, playlist.Count) : (_currentTrackIndex - 1 + playlist.Count) % playlist.Count;
        SelectTrack(prevIndex, _isPlaying);
    }

    public void SelectTrack(int index, bool playImmediately)
    {
        if (index < 0 || index >= playlist.Count) return;
        _currentTrackIndex = index;
        radioSource.clip = playlist[index];
        if (playImmediately) radioSource.Play();
        if (stationText) stationText.text = "FM " + (index + 1) + " - " + playlist[index].name;
    }

    // --- SES SÝSTEMÝ ---
    public void OnVolumeKnobRotated(float value)
    {
        if (volumeSlider) volumeSlider.value = value;
    }

    public void OnVolumeSliderChanged(float value)
    {
        if (radioSource) radioSource.volume = value / 100f;
        UpdateVolumeVisuals(value);
    }

    void UpdateVolumeVisuals(float value)
    {
        if (volumeLevels == null || volumeLevels.Length == 0) return;
        int activeCount = Mathf.Clamp(Mathf.CeilToInt(value / 20f), 0, volumeLevels.Length);
        if (value <= 1f) activeCount = 0;

        for (int i = 0; i < volumeLevels.Length; i++)
        {
            if (volumeLevels[i] != null)
                volumeLevels[i].SetActive(i < activeCount);
        }
    }

    public void OnRadioKnobRotated(float value)
    {
        if (playlist.Count == 0 || _isUpdatingFromKnob) return;
        float segmentSize = 100f / playlist.Count;
        int newIndex = Mathf.Clamp(Mathf.FloorToInt(value / segmentSize), 0, playlist.Count - 1);
        if (newIndex != _currentTrackIndex)
        {
            _isUpdatingFromKnob = true;
            SelectTrack(newIndex, _isPlaying);
            _isUpdatingFromKnob = false;
        }
    }
}