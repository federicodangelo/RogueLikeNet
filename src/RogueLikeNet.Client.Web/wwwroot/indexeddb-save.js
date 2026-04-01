// indexeddb-save.js — IndexedDB-backed save game persistence for the web build.
// Stores game saves (slots, world metadata, chunks, players) in the browser's
// IndexedDB so they survive across sessions, unlike the in-memory provider.

const DB_NAME = 'RogueLikeNet_SaveGames';
const DB_VERSION = 1;

export function createIndexedDbSave() {
    /** @type {IDBDatabase | null} */
    let _db = null;

    // ── Database lifecycle ────────────────────────────────────────

    function open() {
        if (_db) return Promise.resolve();
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, DB_VERSION);
            req.onupgradeneeded = (e) => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains('slots')) db.createObjectStore('slots');
                if (!db.objectStoreNames.contains('worldMeta')) db.createObjectStore('worldMeta');
                if (!db.objectStoreNames.contains('chunks')) db.createObjectStore('chunks');
                if (!db.objectStoreNames.contains('players')) db.createObjectStore('players');
            };
            req.onsuccess = (e) => { _db = e.target.result; resolve(); };
            req.onerror = (e) => reject(e.target.error);
        });
    }

    // ── Bulk load (called once at startup) ────────────────────────

    /** Returns JSON array of SaveSlotInfo objects. */
    function loadAllSlots() {
        return new Promise((resolve, reject) => {
            const t = _db.transaction('slots', 'readonly');
            const req = t.objectStore('slots').getAll();
            req.onsuccess = () => resolve(JSON.stringify(req.result));
            req.onerror = (e) => reject(e.target.error);
        });
    }

    /** Returns JSON object: { slotId: WorldSaveData, ... } */
    function loadAllWorldMeta() {
        return new Promise((resolve, reject) => {
            const t = _db.transaction('worldMeta', 'readonly');
            const store = t.objectStore('worldMeta');
            const result = {};
            const req = store.openCursor();
            req.onsuccess = (e) => {
                const cursor = e.target.result;
                if (!cursor) { resolve(JSON.stringify(result)); return; }
                result[cursor.key] = cursor.value;
                cursor.continue();
            };
            req.onerror = (e) => reject(e.target.error);
        });
    }

    /** Returns JSON object: { slotId: [ChunkSaveEntry, ...], ... } */
    function loadAllChunks() {
        return new Promise((resolve, reject) => {
            const t = _db.transaction('chunks', 'readonly');
            const store = t.objectStore('chunks');
            const grouped = {};
            const req = store.openCursor();
            req.onsuccess = (e) => {
                const cursor = e.target.result;
                if (!cursor) { resolve(JSON.stringify(grouped)); return; }
                const sep = cursor.key.indexOf('|');
                const slotId = cursor.key.substring(0, sep);
                (grouped[slotId] ??= []).push(cursor.value);
                cursor.continue();
            };
            req.onerror = (e) => reject(e.target.error);
        });
    }

    /** Returns JSON object: { slotId: [PlayerSaveData, ...], ... } */
    function loadAllPlayers() {
        return new Promise((resolve, reject) => {
            const t = _db.transaction('players', 'readonly');
            const store = t.objectStore('players');
            const grouped = {};
            const req = store.openCursor();
            req.onsuccess = (e) => {
                const cursor = e.target.result;
                if (!cursor) { resolve(JSON.stringify(grouped)); return; }
                const sep = cursor.key.indexOf('|');
                const slotId = cursor.key.substring(0, sep);
                (grouped[slotId] ??= []).push(cursor.value);
                cursor.continue();
            };
            req.onerror = (e) => reject(e.target.error);
        });
    }

    // ── Individual writes (fire-and-forget from C#) ───────────────

    /** Upsert a save slot. json = serialized SaveSlotInfo. */
    function saveSlot(json) {
        const obj = JSON.parse(json);
        return new Promise((resolve, reject) => {
            const t = _db.transaction('slots', 'readwrite');
            t.objectStore('slots').put(obj, obj.SlotId);
            t.oncomplete = () => resolve();
            t.onerror = (e) => reject(e.target.error);
        });
    }

    /** Delete a save slot and all associated data across all stores. */
    function deleteSlot(slotId) {
        return new Promise((resolve, reject) => {
            const t = _db.transaction(['slots', 'worldMeta', 'chunks', 'players'], 'readwrite');
            t.objectStore('slots').delete(slotId);
            t.objectStore('worldMeta').delete(slotId);
            const range = IDBKeyRange.bound(slotId + '|', slotId + '|\uffff');
            t.objectStore('chunks').delete(range);
            t.objectStore('players').delete(range);
            t.oncomplete = () => resolve();
            t.onerror = (e) => reject(e.target.error);
        });
    }

    /** Upsert world metadata for a slot. json = serialized WorldSaveData. */
    function saveWorldMeta(slotId, json) {
        const obj = JSON.parse(json);
        return new Promise((resolve, reject) => {
            const t = _db.transaction('worldMeta', 'readwrite');
            t.objectStore('worldMeta').put(obj, slotId);
            t.oncomplete = () => resolve();
            t.onerror = (e) => reject(e.target.error);
        });
    }

    /** Batch-upsert chunks. chunksJson = serialized List<ChunkSaveEntry>. */
    function saveChunks(slotId, chunksJson) {
        const chunks = JSON.parse(chunksJson);
        return new Promise((resolve, reject) => {
            const t = _db.transaction('chunks', 'readwrite');
            const store = t.objectStore('chunks');
            for (const chunk of chunks) {
                const key = slotId + '|' + chunk.ChunkX + ',' + chunk.ChunkY + ',' + chunk.ChunkZ;
                store.put(chunk, key);
            }
            t.oncomplete = () => resolve();
            t.onerror = (e) => reject(e.target.error);
        });
    }

    /** Batch-upsert players. playersJson = serialized List<PlayerSaveData>. */
    function savePlayers(slotId, playersJson) {
        const players = JSON.parse(playersJson);
        return new Promise((resolve, reject) => {
            const t = _db.transaction('players', 'readwrite');
            const store = t.objectStore('players');
            for (const player of players) {
                const key = slotId + '|' + player.PlayerName;
                store.put(player, key);
            }
            t.oncomplete = () => resolve();
            t.onerror = (e) => reject(e.target.error);
        });
    }

    return {
        open,
        loadAllSlots, loadAllWorldMeta, loadAllChunks, loadAllPlayers,
        saveSlot, deleteSlot, saveWorldMeta, saveChunks, savePlayers,
    };
}
