# Changelog

## 1.3.1

Fixes:

- The roster, scenarios and profile edits to attacks, resistances and weak spots are saved again, and guests
  now receive the host's arena rules. Unity's JsonUtility silently dropped nested data, so the mod now writes
  JSON through Newtonsoft.Json.
- A profile saved by 1.3.0 no longer resets the creature's resistances to "normal".
- A click no longer lands on a neighboring button when a list changes between frames, for example when a fighter
  dies or a group is added.
- The "Getting control N's position" errors after clicking buttons that change the window are fixed.
- Fighters drawn into the fight by another player are now recognized by every client and counted in the results.
- A death with a death animation was reported by every client, which produced duplicate kill feed lines and
  kills credited to "the elements"; only the owner reports it now.
- With friendly fire disabled, healers and support creatures can heal and buff their allies again.
- The server hands the fighters of a player who has left to a player nearby instead of leaving them frozen.
- A released intruder gets its world state back: night spawn, raid membership, loot, fleeing and sleeping.
- The camera key moved from F9 to F3, because the game uses F9 to switch the gamepad layout; an F9 already saved
  in the config is changed to F3.
- Esc in a text field of the mod no longer opens the game menu. The `[` `]` `-` `=` keys work only while an arena
  is deployed and no longer fire while a map pin name or a build search is being typed.
- The camera no longer turns while the mouse moves toward a button; looking around requires the right mouse
  button.
- Closing the game menu no longer resets the arena's slow motion.
- Over water, the flyer ceiling is measured from the water surface rather than the sea floor.
- Loading a scenario keeps the character, camera and overlay settings.
- The star look correction applies to arena fighters only.

Исправления:

- Состав, сценарии и правки атак, сопротивлений и слабых мест в профилях снова сохраняются, а гости получают
  правила арены хозяина. JsonUtility в Unity молча терял вложенные данные, поэтому мод теперь пишет JSON через
  Newtonsoft.Json.
- Профиль, сохранённый в 1.3.0, больше не сбрасывает существу сопротивления на «обычные».
- Щелчок больше не попадает в соседнюю кнопку, если список изменился между кадрами, например когда боец погиб
  или добавлена группа.
- Устранены ошибки «Getting control N's position», возникавшие после кнопок, которые меняют окно.
- Бойцы, вовлечённые в бой другим игроком, теперь распознаются всеми клиентами и учитываются в итогах.
- Смерть с анимацией сообщал каждый клиент, из-за чего в ленте убийств появлялись дубли, а убийства
  записывались на «стихию»; теперь о ней сообщает только владелец.
- При выключенном дружественном огне лекари и поддержка снова лечат и усиливают союзников.
- Бойцов ушедшего игрока сервер передаёт игроку рядом, а не оставляет замороженными.
- Отпущенному чужаку возвращается его состояние в мире: ночное появление, участие в набеге, добыча, бегство
  и сон.
- Клавиша камеры перенесена с F9 на F3, поскольку F9 игра переключает раскладку геймпада; сохранённая
  в настройках F9 заменяется на F3.
- Esc в поле ввода мода больше не открывает меню игры. Клавиши `[` `]` `-` `=` работают только при развёрнутой
  арене и не срабатывают, пока набирается подпись метки на карте или поиск постройки.
- Камера больше не поворачивается, пока мышь движется к кнопке; обзор требует зажатой правой кнопки.
- Закрытие меню игры больше не сбрасывает замедление времени арены.
- Над водой потолок для летающих отсчитывается от поверхности воды, а не от дна.
- Загрузка сценария не меняет настройки персонажа, камеры и оверлея.
- Поправка облика по звёздам применяется только к бойцам арены.

## 1.3.0

First public release; the README describes the full feature set. The interface is available in English
and Russian.

Первый публичный выпуск; возможности описаны в README. Интерфейс доступен на английском и русском.
