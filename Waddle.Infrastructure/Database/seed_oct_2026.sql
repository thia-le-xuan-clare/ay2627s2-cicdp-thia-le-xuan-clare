-- Waddle seed data for October 2026
-- Safe to re-run the daily_history part; events are deleted for Oct first so you don't get doubles.
PRAGMA foreign_keys = ON;
BEGIN;

DELETE FROM warning_events WHERE day_id IN (SELECT day_id FROM daily_history WHERE date BETWEEN '2026-10-01' AND '2026-10-31');

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-01', 3900) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-01T09:44:31.0000000+08:00' FROM daily_history WHERE date='2026-10-01';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-01T10:03:23.0000000+08:00' FROM daily_history WHERE date='2026-10-01';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-01T10:50:57.0000000+08:00' FROM daily_history WHERE date='2026-10-01';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-01T11:48:30.0000000+08:00' FROM daily_history WHERE date='2026-10-01';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-02', 10800) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-02T09:42:55.0000000+08:00' FROM daily_history WHERE date='2026-10-02';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-02T09:43:15.0000000+08:00' FROM daily_history WHERE date='2026-10-02';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-02T09:59:52.0000000+08:00' FROM daily_history WHERE date='2026-10-02';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-02T10:30:02.0000000+08:00' FROM daily_history WHERE date='2026-10-02';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-02T10:59:48.0000000+08:00' FROM daily_history WHERE date='2026-10-02';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-03', 10800) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-03T09:31:37.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-03T09:31:57.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-03T10:06:39.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-03T10:06:59.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-03T10:56:25.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-03T11:11:56.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-03T12:11:48.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-03T12:48:35.0000000+08:00' FROM daily_history WHERE date='2026-10-03';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-03T12:48:55.0000000+08:00' FROM daily_history WHERE date='2026-10-03';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-04', 3900) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-04T09:32:06.0000000+08:00' FROM daily_history WHERE date='2026-10-04';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-05', 10800) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-05T09:56:26.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-05T10:15:27.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-05T10:39:02.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-05T10:57:05.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-05T11:26:58.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-05T11:48:09.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-05T11:48:29.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-05T12:30:57.0000000+08:00' FROM daily_history WHERE date='2026-10-05';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,3,'2026-10-05T12:31:17.0000000+08:00' FROM daily_history WHERE date='2026-10-05';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-06', 14400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-06T09:17:45.0000000+08:00' FROM daily_history WHERE date='2026-10-06';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-06T09:49:06.0000000+08:00' FROM daily_history WHERE date='2026-10-06';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,3,'2026-10-06T09:49:26.0000000+08:00' FROM daily_history WHERE date='2026-10-06';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-07', 5400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-07T09:56:18.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,3,'2026-10-07T09:56:38.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-07T10:41:04.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-07T11:28:07.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-07T11:45:19.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-07T11:45:39.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-07T12:00:52.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-07T12:58:14.0000000+08:00' FROM daily_history WHERE date='2026-10-07';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-07T12:58:34.0000000+08:00' FROM daily_history WHERE date='2026-10-07';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-08', 600) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-08T09:56:42.0000000+08:00' FROM daily_history WHERE date='2026-10-08';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-08T10:13:00.0000000+08:00' FROM daily_history WHERE date='2026-10-08';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-08T10:13:20.0000000+08:00' FROM daily_history WHERE date='2026-10-08';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-08T10:57:13.0000000+08:00' FROM daily_history WHERE date='2026-10-08';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-09', 5400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-09T09:21:43.0000000+08:00' FROM daily_history WHERE date='2026-10-09';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-09T09:38:58.0000000+08:00' FROM daily_history WHERE date='2026-10-09';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,3,'2026-10-09T09:39:18.0000000+08:00' FROM daily_history WHERE date='2026-10-09';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-09T10:22:51.0000000+08:00' FROM daily_history WHERE date='2026-10-09';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-10', 5400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-10T09:36:57.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-10T10:29:17.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-10T10:29:37.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-10T10:51:51.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-10T10:52:11.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-10T11:27:44.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-10T12:23:24.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-10T13:04:44.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-10T13:41:19.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-10T14:31:28.0000000+08:00' FROM daily_history WHERE date='2026-10-10';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-10T14:31:48.0000000+08:00' FROM daily_history WHERE date='2026-10-10';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-11', 0) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-12', 600) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-12T09:24:49.0000000+08:00' FROM daily_history WHERE date='2026-10-12';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-13', 7500) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-13T09:28:40.0000000+08:00' FROM daily_history WHERE date='2026-10-13';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-13T10:13:23.0000000+08:00' FROM daily_history WHERE date='2026-10-13';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-14', 14400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-14T09:15:27.0000000+08:00' FROM daily_history WHERE date='2026-10-14';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-14T09:37:17.0000000+08:00' FROM daily_history WHERE date='2026-10-14';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-14T10:06:49.0000000+08:00' FROM daily_history WHERE date='2026-10-14';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-14T10:28:36.0000000+08:00' FROM daily_history WHERE date='2026-10-14';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-15', 2400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-15T09:57:27.0000000+08:00' FROM daily_history WHERE date='2026-10-15';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-15T09:57:47.0000000+08:00' FROM daily_history WHERE date='2026-10-15';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-16', 2400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-16T09:50:44.0000000+08:00' FROM daily_history WHERE date='2026-10-16';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-16T09:51:04.0000000+08:00' FROM daily_history WHERE date='2026-10-16';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-16T10:38:48.0000000+08:00' FROM daily_history WHERE date='2026-10-16';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-16T11:22:16.0000000+08:00' FROM daily_history WHERE date='2026-10-16';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-16T11:22:36.0000000+08:00' FROM daily_history WHERE date='2026-10-16';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-17', 0) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-18', 0) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-19', 600) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-19T09:32:50.0000000+08:00' FROM daily_history WHERE date='2026-10-19';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-19T10:23:35.0000000+08:00' FROM daily_history WHERE date='2026-10-19';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-19T11:18:26.0000000+08:00' FROM daily_history WHERE date='2026-10-19';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-19T11:18:46.0000000+08:00' FROM daily_history WHERE date='2026-10-19';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-19T11:50:54.0000000+08:00' FROM daily_history WHERE date='2026-10-19';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-19T11:51:14.0000000+08:00' FROM daily_history WHERE date='2026-10-19';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-19T12:50:08.0000000+08:00' FROM daily_history WHERE date='2026-10-19';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-20', 14400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-20T09:32:52.0000000+08:00' FROM daily_history WHERE date='2026-10-20';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-21', 600) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-21T09:56:08.0000000+08:00' FROM daily_history WHERE date='2026-10-21';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-21T09:56:28.0000000+08:00' FROM daily_history WHERE date='2026-10-21';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-22', 5400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-22T09:30:00.0000000+08:00' FROM daily_history WHERE date='2026-10-22';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-22T10:19:25.0000000+08:00' FROM daily_history WHERE date='2026-10-22';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-22T10:19:45.0000000+08:00' FROM daily_history WHERE date='2026-10-22';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-22T10:36:20.0000000+08:00' FROM daily_history WHERE date='2026-10-22';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-22T11:13:11.0000000+08:00' FROM daily_history WHERE date='2026-10-22';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-22T11:13:31.0000000+08:00' FROM daily_history WHERE date='2026-10-22';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-23', 1500) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-23T09:50:11.0000000+08:00' FROM daily_history WHERE date='2026-10-23';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-24', 0) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-25', 0) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-26', 1500) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-26T09:40:52.0000000+08:00' FROM daily_history WHERE date='2026-10-26';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-26T10:01:35.0000000+08:00' FROM daily_history WHERE date='2026-10-26';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-26T10:01:55.0000000+08:00' FROM daily_history WHERE date='2026-10-26';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-26T10:24:39.0000000+08:00' FROM daily_history WHERE date='2026-10-26';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-27', 14400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-27T09:43:30.0000000+08:00' FROM daily_history WHERE date='2026-10-27';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-27T10:39:17.0000000+08:00' FROM daily_history WHERE date='2026-10-27';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-27T11:01:09.0000000+08:00' FROM daily_history WHERE date='2026-10-27';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-27T11:49:44.0000000+08:00' FROM daily_history WHERE date='2026-10-27';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-28', 7500) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-28T09:30:08.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-28T09:52:35.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-28T10:45:13.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-28T11:45:05.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-28T12:09:23.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,3,'2026-10-28T12:09:43.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-28T13:04:34.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-28T13:04:54.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-28T13:50:04.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-28T14:20:17.0000000+08:00' FROM daily_history WHERE date='2026-10-28';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-28T14:20:37.0000000+08:00' FROM daily_history WHERE date='2026-10-28';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-29', 14400) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-29T09:29:05.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-29T10:02:57.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-29T10:33:50.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-29T11:02:07.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-29T11:02:27.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-29T12:01:10.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-29T12:47:33.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-29T12:47:53.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-29T13:26:36.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-29T14:11:40.0000000+08:00' FROM daily_history WHERE date='2026-10-29';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-29T14:12:00.0000000+08:00' FROM daily_history WHERE date='2026-10-29';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-30', 3900) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-30T09:29:47.0000000+08:00' FROM daily_history WHERE date='2026-10-30';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-30T10:27:26.0000000+08:00' FROM daily_history WHERE date='2026-10-30';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,1,'2026-10-30T10:27:46.0000000+08:00' FROM daily_history WHERE date='2026-10-30';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,1,'2026-10-30T11:09:33.0000000+08:00' FROM daily_history WHERE date='2026-10-30';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,3,'2026-10-30T12:08:23.0000000+08:00' FROM daily_history WHERE date='2026-10-30';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,2,2,'2026-10-30T12:08:43.0000000+08:00' FROM daily_history WHERE date='2026-10-30';
INSERT INTO warning_events (day_id, level_id, outcome_id, occurred_at) SELECT day_id,1,4,'2026-10-30T12:59:38.0000000+08:00' FROM daily_history WHERE date='2026-10-30';

INSERT INTO daily_history (date, seconds_good) VALUES ('2026-10-31', 0) ON CONFLICT(date) DO UPDATE SET seconds_good = excluded.seconds_good;
COMMIT;