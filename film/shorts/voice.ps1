param([string]$Manifest,[string]$Destination,[string]$FilmId="",[int]$SceneIndex=-1)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$data=([IO.File]::ReadAllText($Manifest,[Text.Encoding]::UTF8)|ConvertFrom-Json)
$voice=New-Object System.Speech.Synthesis.SpeechSynthesizer
$voice.SelectVoice('Microsoft Huihui Desktop');$voice.Rate=1
try{
    foreach($film in $data.films){if($FilmId -and $film.id -ne $FilmId){continue}
        $directory=Join-Path $Destination $film.id;New-Item -ItemType Directory -Path $directory -Force|Out-Null
        foreach($scene in $film.scenes){if(!$scene.voice -or ($SceneIndex -ge 0 -and $scene.index -ne $SceneIndex)){continue};$file=Join-Path $directory ('voice-'+$scene.index+'.wav');$voice.SetOutputToWaveFile($file);$voice.Speak([string]$scene.voice);$voice.SetOutputToNull()}
    }
}finally{$voice.Dispose()}
