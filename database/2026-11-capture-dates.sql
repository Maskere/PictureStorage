-- Run once against the PictureDB database before deploying the capture-date build.
ALTER TABLE public.pictures
    ADD COLUMN IF NOT EXISTS captured_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS capture_date_checked boolean NOT NULL DEFAULT false;

-- Rows inserted by the new API are checked during upload. Existing rows are
-- backfilled in the background by CaptureDateBackfillService.
