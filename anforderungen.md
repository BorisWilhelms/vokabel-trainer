 # Vokabel Trainer

## Fachliche Anforderungen

- Unterstuetzung mehrere Sprachen
- Vokabeln Listen
    - Benutzer koennen Listen anlegen
    - Listen haben einen Name
    - Ein Liste hat nur zwei Sprachen
    - Beinhalten ein oder mehrere Vokabeln und deren Uebersetzung
    - Vokabeln werden estmal manuell eingegeben.
- Vokabeltraining
    - Benutzer waehlen eine Liste aus
    - Das System fragt die Vokabeln ab.
    - Die Reihnfolge ist anfaenglich zufaellig.
    - Auch die Sprache ist zufaellig. Mal von z.b. Deutsch -> English, mal English -> Deutsch
    - Das System soll "intelliegent" sein und ueber die Zeit verfolgen welche Vokabeln richtig sind und welche nicht
    - Das System soll ueber die Zeit die Reihnfolge adaptieren und dann vermehrt falsche Vokabeln abfragen
    - Benutzer sollen den Lernfortschritt sehen koennen
        - Wie oft wurde die Liste trainiert
        - Wie erfolgreich war das Training, etc.

## Technische Anforderungen

- Web Anwendung
- Mobilefriendly
    - Alles muss auf einem Smartphone funktionieren
- ASP.NET Core
- Datenbank sqlite
- Frontend sollte mit in der ASP.NET Core Anwendung liegen
- Keine Benutzerverwaltun. Whitelist mit Benutzern
    - Bei ersten Login muss der Benutzer ein passwort vergeben.

