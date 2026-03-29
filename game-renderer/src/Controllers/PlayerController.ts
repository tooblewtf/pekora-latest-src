import express from "ultimate-express";
import Config from "../Utilities/Libraries/Config.js";
import Valid from "../Utilities/Middleware/ValidateDto.js";
import {PlayerRenderRequest} from "../Utilities/Dto/Catalog.js";
import {Console, List} from "../Utilities/Libraries/CS.js";
import {QueueBox} from "../Utilities/Libraries/Queue.js";

import AvatarTemplate from "../../scripts/Avatar.json" with {type: "json"};
import HeadshotTemplate from "../../scripts/Closeup.json" with {type: "json"};
import {BaseJson, RequestRCCBase, RequestRCCBaseXMLData} from "./BaseController.js";

const router = express.Router();
const box = new QueueBox<express.Response>(`PlayerBox`, Config.Ports.RCC.Player);

router.post("/thumbnail", Valid(PlayerRenderRequest), async (req, res) => {
    const xml: BaseJson = JSON.parse(JSON.stringify(AvatarTemplate));
    const charAppUrl = `${Config.BaseUrl}v1.1/avatar-fetch?placeId=0&userId=${req.body.userId}`;
    xml.Settings.Arguments[0] = Config.BaseUrl;
    xml.Settings.Arguments[1] = charAppUrl;
    xml.Settings.Arguments[3] = 840;
    xml.Settings.Arguments[4] = 840;
    Console.Debug(`Queueing player thumbnail request with UserId ${req.body.userId}`);
    return await box.Enqueue((port: number) => RequestRCCBase(
        req,
        res,
        xml,
        port,
        "Player thumbnail"
    ));
});

router.post("/thumbnail-3d", Valid(PlayerRenderRequest), async (req, res) => {
    const xml: BaseJson = JSON.parse(JSON.stringify(AvatarTemplate));
    const charAppUrl = `${Config.BaseUrl}v1.1/avatar-fetch?placeId=0&userId=${req.body.userId}`;
    xml.Settings.Arguments[0] = Config.BaseUrl;
    xml.Settings.Arguments[1] = charAppUrl;
    xml.Settings.Arguments[2] = "OBJ";
    xml.Settings.Arguments[3] = 352;
    xml.Settings.Arguments[4] = 352;
    Console.Debug(`Queueing 3D Player thumbnail request with UserId ${req.body.userId}`);
    return await box.Enqueue((port: number) => RequestRCCBase(
        req,
        res,
        xml,
        port,
        "3D Player thumbnail"
    ));
});

router.post("/headshot", Valid(PlayerRenderRequest), async (req, res) => {
    const xml: BaseJson = JSON.parse(JSON.stringify(HeadshotTemplate));
    const charAppUrl = `${Config.BaseUrl}v1.1/avatar-fetch?placeId=0&userId=${req.body.userId}`;
    xml.Settings.Arguments[0] = Config.BaseUrl;
    xml.Settings.Arguments[1] = charAppUrl;
    xml.Settings.Arguments[3] = 720;
    xml.Settings.Arguments[4] = 720;
    Console.Debug(`Queueing player headshot request with UserId ${req.body.userId}`);
    return await box.Enqueue((port: number) => RequestRCCBase(
        req,
        res,
        xml,
        port,
        "Player headshot"
    ));
});

export default router;

export interface Thumbnail3DRCC {
    camera: {
        position: {
            x: number;
            y: number;
            z: number;
        },
        direction: {
            x: number;
            y: number;
            z: number;
        },
        fov: number
    },
    AABB: {
        min: {
            x: number;
            y: number;
            z: number;
        },
        max: {
            x: number;
            y: number;
            z: number;
        },
    },
    files: {
        "scene.obj"?: {
            content: string;
        },
        "scene.mtl"?: {
            content: string;
        },
    },
}

export interface Thumbnail3DResponse {
    camera: {
        position: {
            x: number;
            y: number;
            z: number;
        },
        direction: {
            x: number;
            y: number;
            z: number;
        },
        fov: number
    },
    aabb: {
        min: {
            x: number;
            y: number;
            z: number;
        },
        max: {
            x: number;
            y: number;
            z: number;
        },
    },
    mtl: string,
    obj: string,
    textures: string[],
}
