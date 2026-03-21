# Vokabel Trainer — Design Spec

## Ueberblick

Web-basierter Vokabeltrainer fuer eine Gymnasialschuelerin (9. Klasse), Schwerpunkt Latein und Englisch. Sitzungsbasiertes Leitner-System, mobile-first PWA, einfache Bedienung.

## Architektur

**Solution mit drei Projekten:**

- **VokabelTrainer.Api** — ASP.NET Core Web API. Controller, Services, EF Core + SQLite. Hostet die Blazor WASM App als statische Files. Gesamte Geschaeftslogik liegt hier.
- **VokabelTrainer.Client** — Blazor WASM PWA. Reine UI, spricht ausschliesslich mit der API. Installierbar auf dem Homescreen.
- **VokabelTrainer.Shared** — DTOs und Contracts (Request/Response Models). Keine Logik.

**Schichtentrennung im API-Projekt:**
- Controller (duenn: Validierung, Service-Aufruf, Response) → Services (Geschaeftslogik) → EF Core DbContext
- Frontend ist austauschbar — die gesamte Logik liegt hinter der API.

## Authentifizierung

- Cookie-basierte Authentifizierung. Kein ASP.NET Identity — eigener Auth-Handler.
- Beim ersten Login mit bekanntem Benutzernamen wird ein Passwort gesetzt. Danach Login mit Benutzername + Passwort.
- **Admin-Setup:** Wenn die DB keine Benutzer enthaelt, wird der erste Login automatisch zum Admin. Admin setzt sein Passwort wie jeder andere User.
- **Rollen:** Zwei Rollen — Admin und User. Rolle wird im User-Modell gespeichert.

### Admin-Oberflaeche (minimal)

- **Benutzer:** Anlegen, loeschen, Passwort zuruecksetzen
- **Sprachen:** Anlegen, bearbeiten, loeschen. Pro Sprache: Code, Anzeigename, Flaggen-SVG.
- Kein Seed ueber appsettings.json noetig — alles ueber die Admin-UI. Beim ersten Start (leere DB) muessen nach dem Admin-Setup zuerst Sprachen angelegt werden, bevor Listen erstellt werden koennen.

## Datenmodell

### User
| Feld | Typ | Beschreibung |
|------|-----|-------------|
| Id | int (PK) | |
| Username | string | |
| PasswordHash | string? | Null bis zum ersten Login |
| IsInitialized | bool | Passwort gesetzt? |
| Role | enum | Admin / User |

### Language
| Feld | Typ | Beschreibung |
|------|-----|-------------|
| Id | int (PK) | |
| Code | string | ISO-artig, z.B. "la", "en", "de". Unique. |
| DisplayName | string | z.B. "Latein", "Englisch", "Deutsch" |
| FlagSvg | string? | SVG-Markup fuer die Flagge |

Wird ueber die Admin-UI gepflegt. Beim Erstellen einer Liste waehlt der User aus den vorhandenen Sprachen.

### VocabularyList
| Feld | Typ | Beschreibung |
|------|-----|-------------|
| Id | int (PK) | |
| UserId | int (FK) | Besitzer |
| Name | string | z.B. "Latein Lektion 12" |
| SourceLanguageId | int (FK) | Referenz auf Language |
| TargetLanguageId | int (FK) | Referenz auf Language |
| CreatedAt | datetime | |

Listen sind privat pro Benutzer.

### Vocabulary
| Feld | Typ | Beschreibung |
|------|-----|-------------|
| Id | int (PK) | |
| ListId | int (FK) | |
| Term | string | z.B. "res" |
| Translations | string (JSON) | z.B. `["Sache", "Ding", "Angelegenheit"]` |

Translations als JSON-Array gespeichert. Bei der Eingabe werden verschiedene Trennzeichen akzeptiert (Komma, Semikolon, Pipe) und ins Array geparst. Der Separator zwischen Term und Translations ist ausschliesslich `=`.

**Loeschen:** Wird eine Vokabel aus einer Liste entfernt, werden zugehoerige BoxEntry- und TrainingAnswer-Eintraege kaskadierend geloescht.

### BoxEntry (Leitner-Stand)
| Feld | Typ | Beschreibung |
|------|-----|-------------|
| Id | int (PK) | |
| UserId | int (FK) | |
| VocabularyId | int (FK) | |
| Box | int | 1-5 |
| SessionsUntilReview | int | Countdown bis zur naechsten Abfrage |

Unique Constraint auf (UserId, VocabularyId). Wird beim ersten Training einer Liste automatisch angelegt (alle Vokabeln starten in Box 1, SessionsUntilReview = 0).

**Sitzungsbasiertes Leitner statt zeitbasiert:**
- Box 1: jede Session (Intervall 1)
- Box 2: jede 2. Session (Intervall 2)
- Box 3: jede 4. Session (Intervall 4)
- Box 4: jede 8. Session (Intervall 8)
- Box 5: jede 16. Session (Intervall 16)

Nach jeder abgeschlossenen Session wird SessionsUntilReview fuer **alle** Vokabeln der Liste um 1 reduziert — auch fuer Vokabeln die in dieser Session nicht abgefragt wurden (z.B. bei begrenzter Vokabelanzahl). Die Session zaehlt als "eine Runde" fuer die gesamte Liste. Vokabeln mit SessionsUntilReview <= 0 sind faellig.

### TrainingSession
| Feld | Typ | Beschreibung |
|------|-----|-------------|
| Id | int (PK) | |
| UserId | int (FK) | |
| ListId | int? (FK) | Null bei listenuebergreifendem Training |
| Mode | enum | SinglePass / Endlos |
| StartedAt | datetime | |
| CompletedAt | datetime? | |
| TotalQuestions | int | Denormalisiert fuer schnellen Zugriff |
| CorrectAnswers | int | Denormalisiert fuer schnellen Zugriff |

### TrainingAnswer
| Feld | Typ | Beschreibung |
|------|-----|-------------|
| Id | int (PK) | |
| SessionId | int (FK) | |
| VocabularyId | int (FK) | |
| Direction | enum | SourceToTarget / TargetToSource |
| GivenAnswer | string | |
| IsCorrect | bool | |
| AnsweredAt | datetime | |

## Trainings-Flow

### Session starten
1. Benutzer waehlt eine Liste **oder "Alle Listen"** (listenuebergreifendes Training)
2. Benutzer waehlt den Modus:
   - **Einmal durch** — jede faellige Vokabel wird einmal abgefragt
   - **Endlos** — falsch beantwortete Vokabeln kommen zurueck in den Pool, Session endet wenn alle mindestens einmal richtig beantwortet wurden
3. Benutzer kann optional die Anzahl Vokabeln begrenzen (Default: alle faelligen)

**Listenuebergreifendes Training:** Faellige Vokabeln aus allen Listen werden in einen Pool zusammengefasst. Leitner-Logik bleibt identisch. Nach Abschluss wird SessionsUntilReview fuer alle betroffenen Listen reduziert.

### Abfrage-Logik
1. System waehlt naechste Vokabel aus dem Pool (Leitner-Prioritaet: niedrige Boxen zuerst)
2. Zufaellige Richtung: SourceToTarget oder TargetToSource
3. Benutzer tippt Antwort ein
4. Abgleich: case-insensitive, Whitespace getrimmt, strikter Vergleich (keine Akzent-Normalisierung, keine Tippfehler-Toleranz — spaeter erweiterbar). Bei TargetToSource muss eine der Translations matchen, bei SourceToTarget der Term.
5. Sofortiges Feedback: richtig/falsch, bei falsch die korrekte(n) Antwort(en) anzeigen
6. Leitner-Update: richtig = Box + 1 (max 5), falsch = zurueck auf Box 1
7. Im Endlos-Modus: falsche Vokabeln gehen zurueck in den Pool, aber nicht sofort — ein paar andere Vokabeln kommen dazwischen

### Session beenden
- Einmal-durch: nach der letzten Vokabel automatisch
- Endlos: wenn alle richtig, oder manuell ueber "Abbrechen" (bisheriger Stand zaehlt)
- Ergebnis-Screen: Prozent richtig, Liste der falsch beantworteten mit korrekter Antwort

## Fortschrittsanzeige

### Pro Liste
- **Box-Verteilung** — Balkendiagramm: wie viele Vokabeln in Box 1-5. Mit Erklaerung des Leitner-Systems.
- **Erfolgsquote ueber Zeit** — Liniendiagramm: Prozent richtig pro Session
- **Anzahl absolvierte Sessions**

### Problemvokabeln
- Vokabeln die nach mehreren Sessions in Box 1 haengen
- Zeigt an wo gezielt nachgearbeitet werden muss

### Gesamtuebersicht (Dashboard)
- Alle Listen mit Box-Verteilungs-Balken auf einen Blick ("Gesundheit" der Liste)

## UI-Screens

1. **Login** — Benutzername + Passwort. Erster Login setzt Passwort. Erster User ueberhaupt wird Admin.
2. **Dashboard** — Listenuebersicht mit Leitner-Box-Balken, Flaggen pro Sprache. Buttons: Trainieren, Bearbeiten, Fortschritt. Button fuer neue Liste. Button "Alle trainieren" fuer listenuebergreifendes Training. Admin sieht zusaetzlich Link zur Admin-Oberflaeche.
3. **Liste erstellen/bearbeiten** — Name, Sprachen (Dropdowns), Texteingabe: eine Vokabel pro Zeile im Format `Begriff = Uebersetzung1, Uebersetzung2`. Verschiedene Trennzeichen werden akzeptiert.
4. **Training starten** — Moduswahl (Einmal/Endlos), optionale Vokabelanzahl.
5. **Training** — Karte mit Richtungsanzeige (Flaggen), Eingabefeld, sofortiges Feedback, Box-Update-Anzeige, Fortschrittsanzeige (x/n).
6. **Session-Ergebnis** — Prozent, falsch beantwortete mit Loesung, Buttons fuer "Nochmal" und "Zurueck".
7. **Fortschritt** — Box-Verteilung mit Erklaerung, Erfolgsquote-Verlauf, Problemvokabeln.
8. **Admin** — (nur fuer Admins) Liste aller Benutzer, Buttons: Neuen Benutzer anlegen, Passwort zuruecksetzen, Benutzer loeschen.

## Technische Details

- **Frontend:** Blazor WASM als PWA. Installierbar auf Mobilgeraeten. Mobile-first Design.
- **Backend:** ASP.NET Core Web API
- **Datenbank:** SQLite via EF Core
- **UI-Framework:** MudBlazor (Material Design Komponentenbibliothek, inkl. Charts)
- **Sprach-Flaggen:** SVG-Icons pro Sprache, im Client hinterlegt
- **Deployment:** Einzelne ASP.NET Core Anwendung die alles hostet
- **Offline:** Explizit out of scope. PWA dient nur der Installierbarkeit, nicht der Offline-Nutzung.
- **Lokalisierung:** Alle UI-Texte ueber `IStringLocalizer` / `.resx`-Dateien. Initiale Sprache: Deutsch. Weitere Sprachen spaeter durch Hinzufuegen von `.resx`-Dateien moeglich, ohne Code-Aenderungen.
- **.NET 10**, Nullable Reference Types enabled, TreatWarningsAsErrors in allen Projekten.
